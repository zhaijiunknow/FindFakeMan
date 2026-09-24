using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Core.Runtime.Framework
{
    /// <summary>
    /// 面板内过场：只在**一个 UI 板块**里播 CRT 关机/开机，而不是像 <see cref="ScreenTransition"/> 那样盖全屏。
    ///
    /// 用途：同一个软件窗口"换视图"的场合 —— 例如序幕结束时，只在 mainUI 的 `game` 板块里收屏，
    /// 加载完再在（新场景的）`game` 板块里开机，露出别墅房间。窗口边框、标题栏、右栏 Health/Collect、
    /// 底部工具栏、左栏时间全程不动。
    ///
    /// 实现：在目标板块里临时建 上/下黑边 + 中缝亮线 + 收屏白点（都是该板块的子物体、铺满它、置顶），
    /// 所以效果不会溢出到板块外面，也不需要 Mask。关机在**旧场景**的板块里播，开机在**新场景**的板块里播
    /// —— 每次调用都各自按名字找自己场景里的那一块。
    /// </summary>
    public static class PanelTransition
    {
        /// <summary>mainUI 里那块视图板的名字（房间/新闻都画在这里）。</summary>
        private const string HostName = "game";

        private const float LineFlashHold = 0.12f;
        private const float DotFadeDuration = 0.14f;

        /// <summary>
        /// 关机：黑边从上下合拢到中间，中缝亮线闪一下后收成白点。
        /// 找不到板块时会退化成"等 duration 秒"，保证流程不会卡住。
        /// </summary>
        public static async UniTask CollapseAsync(float duration)
        {
            await PlayAsync(duration, collapsing: true);
        }

        /// <summary>开机：从全黑的一条亮线向上下展开。</summary>
        public static async UniTask ExpandAsync(float duration)
        {
            await PlayAsync(duration, collapsing: false);
        }

        private static async UniTask PlayAsync(float duration, bool collapsing)
        {
            var host = FindHost();
            if (host == null)
            {
                Debug.LogWarning($"[PanelTransition] 当前场景里找不到名为 {HostName} 的板块，转场退化为等待。");
                await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0.01f, duration)));
                return;
            }

            var overlay = Overlay.Create(host);
            var height = Mathf.Max(1f, host.rect.height);
            var total = Mathf.Max(0.01f, duration);

            if (collapsing)
            {
                overlay.SetBars(0f);
                overlay.SetLine(0f);

                var elapsed = 0f;
                while (elapsed < total)
                {
                    var t = Mathf.Clamp01(elapsed / total);
                    var e = Mathf.SmoothStep(0f, 1f, t);
                    overlay.SetBars(height * 0.5f * e);
                    overlay.SetLine(Mathf.Clamp01(0.1f + 0.9f * e));
                    await UniTask.Yield(PlayerLoopTiming.Update);
                    elapsed += Time.unscaledDeltaTime;
                }

                overlay.SetBars(height);
                overlay.SetLine(1f);
                await UniTask.Delay(TimeSpan.FromSeconds(LineFlashHold), ignoreTimeScale: true);
                overlay.SetLine(0f);
                await overlay.PlayDotFadeAsync(height);
                overlay.SetBars(height); // 保持全黑，等场景加载
                return;
            }

            // 开机：先停一条亮线，再向上下展开。
            overlay.SetBars(height);
            overlay.SetLine(1f);
            await UniTask.Delay(TimeSpan.FromSeconds(LineFlashHold), ignoreTimeScale: true);

            var expandElapsed = 0f;
            while (expandElapsed < total)
            {
                var t = Mathf.Clamp01(expandElapsed / total);
                var e = Mathf.SmoothStep(0f, 1f, t);
                overlay.SetBars(height * (1f - e));
                overlay.SetLine(Mathf.Clamp01(1f - e * 1.8f));
                await UniTask.Yield(PlayerLoopTiming.Update);
                expandElapsed += Time.unscaledDeltaTime;
            }

            overlay.SetBars(0f);
            overlay.SetLine(0f);
            overlay.Destroy();
        }

        /// <summary>在当前场景里找那块视图板（优先 Canvas 下的，避免撞上同名的别的东西）。</summary>
        private static RectTransform FindHost()
        {
            RectTransform fallback = null;
            foreach (var rect in UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (rect.name != HostName)
                {
                    continue;
                }

                if (rect.GetComponentInParent<Canvas>() != null)
                {
                    return rect;
                }

                fallback ??= rect;
            }

            return fallback;
        }

        /// <summary>板块里的临时覆盖层（上下黑边 + 中缝亮线 + 白点）。</summary>
        private sealed class Overlay
        {
            private RectTransform top;
            private RectTransform bottom;
            private Image line;
            private Image dot;
            private GameObject root;

            public static Overlay Create(RectTransform host)
            {
                var overlay = new Overlay();

                var rootGo = new GameObject("PanelTransition (runtime)", typeof(RectTransform));
                rootGo.transform.SetParent(host, false);
                rootGo.transform.SetAsLastSibling(); // 盖在板块内容之上
                overlay.root = rootGo;
                var rootRect = (RectTransform)rootGo.transform;
                Stretch(rootRect);

                overlay.top = CreateBar(rootRect, "BarTop", new Vector2(0.5f, 1f));
                overlay.bottom = CreateBar(rootRect, "BarBottom", new Vector2(0.5f, 0f));

                overlay.line = CreateImage(rootRect, "Line", Color.white);
                var lineRect = overlay.line.rectTransform;
                lineRect.anchorMin = new Vector2(0f, 0.5f);
                lineRect.anchorMax = new Vector2(1f, 0.5f);
                lineRect.pivot = new Vector2(0.5f, 0.5f);
                lineRect.anchoredPosition = Vector2.zero;
                lineRect.sizeDelta = new Vector2(0f, 3f);

                overlay.dot = CreateImage(rootRect, "Dot", Color.white);
                var dotRect = overlay.dot.rectTransform;
                dotRect.anchorMin = dotRect.anchorMax = new Vector2(0.5f, 0.5f);
                dotRect.pivot = new Vector2(0.5f, 0.5f);
                dotRect.anchoredPosition = Vector2.zero;
                dotRect.sizeDelta = Vector2.zero;

                return overlay;
            }

            public void SetBars(float height)
            {
                top.sizeDelta = new Vector2(0f, height);
                bottom.sizeDelta = new Vector2(0f, height);
            }

            public void SetLine(float alpha)
            {
                line.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }

            public async UniTask PlayDotFadeAsync(float height)
            {
                var startSize = Mathf.Max(5f, height / 90f);
                var elapsed = 0f;
                while (elapsed < DotFadeDuration)
                {
                    var t = Mathf.Clamp01(elapsed / DotFadeDuration);
                    var size = Mathf.Lerp(startSize, 0f, t);
                    dot.rectTransform.sizeDelta = new Vector2(size, size);
                    dot.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, t * t));
                    await UniTask.Yield(PlayerLoopTiming.Update);
                    elapsed += Time.unscaledDeltaTime;
                }

                dot.rectTransform.sizeDelta = Vector2.zero;
                dot.color = new Color(1f, 1f, 1f, 0f);
            }

            public void Destroy()
            {
                if (root != null)
                {
                    UnityEngine.Object.Destroy(root);
                }
            }

            private static RectTransform CreateBar(RectTransform parent, string name, Vector2 anchor)
            {
                var image = CreateImage(parent, name, Color.black);
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = anchor;
                rect.pivot = anchor;
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                return rect;
            }

            private static Image CreateImage(RectTransform parent, string name, Color color)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                var image = go.GetComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
                return image;
            }

            private static void Stretch(RectTransform rect)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }
    }
}
