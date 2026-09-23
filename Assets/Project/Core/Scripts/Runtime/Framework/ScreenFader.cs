using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Core.Runtime.Framework
{
    /// <summary>
    /// 跨场景转场：不用"纯黑渐隐"，而是复用项目里终端 / CRT 的语言。
    ///
    ///  - 出场（<see cref="CollapseAsync"/>）= **CRT 关机**：上下黑边向中间合拢，
    ///    中缝留一条越来越亮的横线，收拢瞬间闪一下、再收成一个白点消失，扫描线残影随之淡掉；
    ///  - 入场（<see cref="ExpandAsync"/>）= **CRT 开机**：中间一条亮线向上下展开，露出新场景。
    ///
    /// 它是 DontDestroyOnLoad 的独立 Canvas（sortingOrder 很高，盖住一切），
    /// 所以加载新场景时不会被销毁，可以贯穿"旧场景关机 → 加载 → 新场景开机"全过程。
    /// 用 unscaledDeltaTime 驱动（timeScale = 0 也能转场），转场期间挡点击。
    ///
    /// 注：文件名保留为 ScreenFader.cs（改名请直接在编辑器里重命名，meta 会跟着走）。
    /// </summary>
    public static class ScreenTransition
    {
        private const int SortingOrder = 32000;
        private const string RootName = "ScreenTransition (DontDestroyOnLoad)";

        private const float LineFlashHold = 0.12f;   // 收拢/开机前亮线停留（"闪一下"要看得见）
        private const float DotFadeDuration = 0.18f; // 白点收缩消失

        private static CanvasGroup group;
        private static Image clickBlock;
        private static Image scanlines;
        private static RectTransform barTop;
        private static RectTransform barBottom;
        private static Image line;
        private static Image dot;
        private static Sprite scanlineSprite;
        private static bool busy;

        /// <summary>当前画面是否已经被转场盖成全黑。</summary>
        public static bool IsBlack { get; private set; }

        /// <summary>出场：CRT 关机（合拢到全黑）。</summary>
        public static async UniTask CollapseAsync(float duration)
        {
            EnsureOverlay();
            if (group == null)
            {
                return;
            }

            await WaitWhileBusyAsync();
            busy = true;
            try
            {
                group.gameObject.SetActive(true);
                group.alpha = 1f;
                clickBlock.raycastTarget = true;

                var screenHeight = Mathf.Max(1f, Screen.height);
                var thickness = Mathf.Max(2f, screenHeight / 360f);
                line.rectTransform.sizeDelta = new Vector2(0f, thickness);

                SetBarHeights(0f);
                SetLineAlpha(0f);
                SetDotVisible(false);
                scanlines.color = new Color(1f, 1f, 1f, 0.18f);

                var total = Mathf.Max(0.01f, duration);
                var elapsed = 0f;
                while (elapsed < total)
                {
                    var t = Mathf.Clamp01(elapsed / total);
                    var e = Mathf.SmoothStep(0f, 1f, t); // 均匀一点，别"前半不动、最后一瞬收完"
                    SetBarHeights(screenHeight * 0.5f * e);
                    var flicker = 0.8f + 0.2f * Mathf.Sin(elapsed * 70f);
                    SetLineAlpha(Mathf.Clamp01(0.1f + 0.9f * e) * flicker);
                    scanlines.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.18f, 0.05f, e));
                    await UniTask.Yield(PlayerLoopTiming.Update);
                    elapsed += Time.unscaledDeltaTime;
                }

                // 收拢：全黑 + 中缝闪一下
                SetBarHeights(screenHeight * 0.5f + 2f);
                SetLineAlpha(1f);
                await UniTask.Delay(TimeSpan.FromSeconds(LineFlashHold), ignoreTimeScale: true);

                // 亮线收成一个白点消失
                SetLineAlpha(0f);
                await PlayDotFadeAsync(screenHeight, thickness);

                scanlines.color = new Color(1f, 1f, 1f, 0f);
                IsBlack = true;
            }
            finally
            {
                busy = false;
            }
        }

        /// <summary>入场：CRT 开机（从全黑展开）。</summary>
        public static async UniTask ExpandAsync(float duration)
        {
            EnsureOverlay();
            if (group == null)
            {
                return;
            }

            await WaitWhileBusyAsync();
            busy = true;
            try
            {
                group.gameObject.SetActive(true);
                group.alpha = 1f;
                clickBlock.raycastTarget = true;

                var screenHeight = Mathf.Max(1f, Screen.height);
                var thickness = Mathf.Max(2f, screenHeight / 360f);
                line.rectTransform.sizeDelta = new Vector2(0f, thickness);

                // 从全黑开始：先在中间停一条亮线，再向上下展开（这一段就是"开机"的那一拍）。
                SetBarHeights(screenHeight * 0.5f + 2f);
                SetDotVisible(false);
                SetLineAlpha(1f);
                scanlines.color = new Color(1f, 1f, 1f, 0.12f);
                await UniTask.Delay(TimeSpan.FromSeconds(LineFlashHold), ignoreTimeScale: true);

                var total = Mathf.Max(0.01f, duration);
                var elapsed = 0f;
                while (elapsed < total)
                {
                    var t = Mathf.Clamp01(elapsed / total);
                    // 用 smoothstep：从亮线开始"慢慢张开"，不要一开始就弹开（否则画面一瞬间就全露出来，显得很快）。
                    var e = Mathf.SmoothStep(0f, 1f, t);
                    SetBarHeights(screenHeight * 0.5f * (1f - e));
                    SetLineAlpha(Mathf.Clamp01(1f - e * 1.8f));
                    scanlines.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.12f, 0f, e));
                    await UniTask.Yield(PlayerLoopTiming.Update);
                    elapsed += Time.unscaledDeltaTime;
                }

                SetBarHeights(0f);
                SetLineAlpha(0f);
                scanlines.color = new Color(1f, 1f, 1f, 0f);
                clickBlock.raycastTarget = false;
                IsBlack = false;
                group.gameObject.SetActive(false);
            }
            finally
            {
                busy = false;
            }
        }

        /// <summary>立刻变全黑（不做动画，兜底用）。</summary>
        public static void SetBlackImmediate()
        {
            EnsureOverlay();
            if (group == null)
            {
                return;
            }

            group.gameObject.SetActive(true);
            group.alpha = 1f;
            clickBlock.raycastTarget = true;
            scanlines.color = new Color(1f, 1f, 1f, 0f);
            SetLineAlpha(0f);
            SetDotVisible(false);
            SetBarHeights(Mathf.Max(1f, Screen.height) * 0.5f + 2f);
            IsBlack = true;
        }

        // ---- 内部 ----

        private static async UniTask WaitWhileBusyAsync()
        {
            while (busy)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        private static async UniTask PlayDotFadeAsync(float screenHeight, float thickness)
        {
            var startSize = Mathf.Max(6f, screenHeight / 90f);
            var elapsed = 0f;
            while (elapsed < DotFadeDuration)
            {
                var t = Mathf.Clamp01(elapsed / DotFadeDuration);
                var size = Mathf.Lerp(startSize, 0f, t);
                dot.rectTransform.sizeDelta = new Vector2(size, Mathf.Max(thickness, size));
                dot.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, t * t));
                await UniTask.Yield(PlayerLoopTiming.Update);
                elapsed += Time.unscaledDeltaTime;
            }

            SetDotVisible(false);
        }

        private static void SetBarHeights(float height)
        {
            if (barTop != null)
            {
                barTop.sizeDelta = new Vector2(barTop.sizeDelta.x, height);
            }

            if (barBottom != null)
            {
                barBottom.sizeDelta = new Vector2(barBottom.sizeDelta.x, height);
            }
        }

        private static void SetLineAlpha(float alpha)
        {
            if (line != null)
            {
                line.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        private static void SetDotVisible(bool visible)
        {
            if (dot == null)
            {
                return;
            }

            dot.color = new Color(1f, 1f, 1f, visible ? 1f : 0f);
            if (!visible)
            {
                dot.rectTransform.sizeDelta = Vector2.zero;
            }
        }

        private static void EnsureOverlay()
        {
            if (group != null)
            {
                return;
            }

            // 需要 GraphicRaycaster，ClickBlock 才能真的挡住（否则 Canvas 不参与射线检测）。
            var root = new GameObject(RootName, typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(root);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            group = root.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = false;
            // 注意：blocksRaycasts 必须为 true，否则下面 ClickBlock 的 raycastTarget 会被整组屏蔽、挡不住点击。
            group.blocksRaycasts = true;

            // 1) 转场期间挡点击（透明但 raycastTarget = true）
            clickBlock = CreateImage(root.transform, "ClickBlock", new Color(0f, 0f, 0f, 0f));
            StretchFull(clickBlock.rectTransform);
            clickBlock.raycastTarget = false;

            // 2) CRT 扫描线残影
            scanlines = CreateImage(root.transform, "Scanlines", new Color(1f, 1f, 1f, 0f));
            StretchFull(scanlines.rectTransform);
            scanlines.sprite = ScanlineSprite();
            scanlines.type = Image.Type.Tiled;
            scanlines.raycastTarget = false;

            // 3) 上下黑边
            barTop = CreateBar(root.transform, "BarTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            barBottom = CreateBar(root.transform, "BarBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));

            // 4) 中缝亮线
            line = CreateImage(root.transform, "Line", Color.white);
            var lineRect = line.rectTransform;
            lineRect.anchorMin = new Vector2(0f, 0.5f);
            lineRect.anchorMax = new Vector2(1f, 0.5f);
            lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.anchoredPosition = Vector2.zero;
            lineRect.sizeDelta = new Vector2(0f, 3f);
            line.raycastTarget = false;

            // 5) 收屏末端的白点
            dot = CreateImage(root.transform, "Dot", new Color(1f, 1f, 1f, 0f));
            var dotRect = dot.rectTransform;
            dotRect.anchorMin = new Vector2(0.5f, 0.5f);
            dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.anchoredPosition = Vector2.zero;
            dotRect.sizeDelta = Vector2.zero;
            dot.raycastTarget = false;

            root.SetActive(false);
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var image = CreateImage(parent, name, Color.black);
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>生成横向扫描线贴图：每 8px 一条半透明黑线，Tiled 平铺成 CRT 细网格。</summary>
        private static Sprite ScanlineSprite()
        {
            if (scanlineSprite != null)
            {
                return scanlineSprite;
            }

            // 贴图宽一点（32px）平铺时生成的 quad 更少，转场期间不容易卡。
            const int width = 32;
            const int height = 8;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "TransitionScanlineTexture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
            };

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var isScan = y == 0;
                    tex.SetPixel(x, y, isScan ? new Color(0f, 0f, 0f, 0.55f) : new Color(0f, 0f, 0f, 0f));
                }
            }

            tex.Apply();
            scanlineSprite = Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f));
            return scanlineSprite;
        }
    }
}
