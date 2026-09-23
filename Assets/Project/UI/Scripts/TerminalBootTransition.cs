using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Managers;
using Project.Narrative.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
    /// <summary>
    /// 序幕过场：模拟"电脑屏幕关机 → 重新开机自检 → 切换成终端 → 主角眨眼 → 全屏终端"。
    ///
    /// 前提（序幕场景的图层）：
    ///   Canvas/background
    ///     ├─ Image          深色底
    ///     ├─ 2              新闻画面（全屏 rect）= **主角的电脑屏幕**
    ///     ├─ ScreenFx       CRT 扫描线
    ///     ├─ ScreenContent  屏幕内容层（开机自检 + 屏幕内的 mainUI）← 本组件用
    ///     └─ 扣a2           房间（显示器屏幕处镂空）← 天然遮罩，把内容"装进显示器"
    ///
    /// 关键点：
    ///  - **镜头/位置全程不动**，房间与新闻都不挪地方；
    ///  - 关机只塌缩 `2`（新闻），塌缩后露出深色底 = 屏幕黑了，房间照旧；
    ///  - 开机自检与屏幕内的 mainUI 都放在 ScreenContent 下（与 `2` 同一个 rect，所以大小一致）；
    ///  - 只有眨眼的上下黑边是全屏的；眨眼全黑那一瞬把 mainUI 提到 Canvas 顶层并还原成全屏；
    ///  - **整段过场不干涉剧情**：不锁推进、不吞输入，自检文字按自己的节奏打完，
    ///    玩家随时可以点击/空格继续推进对白（字幕条在终端之上，始终可见）。
    ///
    /// 时序：
    ///  1 关机       `2` 原位垂直塌缩成一条线 + 白线亮起
    ///  2 熄灭       白线收成一个点并消失
    ///  3 黑屏       隐藏 `2`（露出深色底）并关掉 CRT 扫描线
    ///  4 开机自检   ScreenContent/BootScreen 亮起 + 自检文字逐行打出（在显示器里）
    ///  5 打开终端   mainUI 显示在显示器里（同一层，同样缩放）
    ///  6 眨眼闭     上下黑边合拢到全黑
    ///  7 偷换       全黑那一刻：mainUI 提到 Canvas 顶层、还原全屏；自检层收起
    ///  8 睁眼       黑边张开 → 全屏终端
    /// </summary>
    public sealed class TerminalBootTransition : MonoBehaviour
    {
        [Header("触发")]
        [Tooltip("VN 播到这个节点时播放一次（默认即玩家选「继续听取简报」后进入的节点）。")]
        [SerializeField] private string triggerNodeId = "briefing_whitewan_001";
        [SerializeField] private bool triggerOnce = true;
        [SerializeField] private bool logBeats = true;

        [Header("图层引用")]
        [Tooltip("新闻画面对象：Canvas/background/2，也就是模拟关机的电脑屏幕。")]
        [SerializeField] private RectTransform newsVisual;
        [Tooltip("屏幕内容层：位于新闻之上、房间(扣a2)之下。它和 2 是同一个 rect（全屏拉伸），" +
                 "所以大小/可见范围与 2 完全一致 —— 加载和终端都显示在这里。")]
        [SerializeField] private RectTransform screenContent;
        [Tooltip("终端本体（Main.prefab 实例根），初始隐藏。")]
        [SerializeField] private RectTransform mainUiRoot;
        [Tooltip("mainUI 变全屏后的父级：Canvas 的 RectTransform。")]
        [SerializeField] private RectTransform canvasRoot;

        [Header("1-2 关机（只关 background/2）")]
        [SerializeField] private RectTransform whiteLine;
        [SerializeField] private CanvasGroup whiteLineGroup;
        [SerializeField] private ScreenFxOverlay crtFx;
        [SerializeField] private float powerOffDuration = 0.16f;
        [Tooltip("塌缩时横向拉伸倍数，做出老电视收屏的感觉。")]
        [SerializeField] private float powerOffWiden = 1.08f;
        [SerializeField] private float flashDuration = 0.1f;

        [Header("4 开机自检")]
        [SerializeField] private GameObject bootRoot;
        [SerializeField] private TMP_Text bootLogText;
        [SerializeField] private string[] bootLines =
        {
            "OKAS TERMINAL BOOT v2.050",
            "ORDER KEEPER ALLIANCE SYSTEM",
            "",
            "> 校验操作员身份 ......",
            "> 操作员 A先生 / 第七行动组 ...... 通过",
            "> 收容箱 0/3    工具包耐久 5",
            "> 检索案件 PX-2050-734 ......",
            "> 目标：白婉 / 26 / 行政专员",
            "> 档案完整性 ...... 0%",
            "> 载入行动简报 ......",
            "",
            "SYSTEM READY"
        };
        [SerializeField] private float bootCharInterval = 0.012f;
        [SerializeField] private float bootLineInterval = 0.08f;
        [SerializeField] private float bootHoldAfter = 0.25f;

        [Header("6-8 眨眼（唯一全屏的部分）")]
        [SerializeField] private RectTransform blinkTopBar;
        [SerializeField] private RectTransform blinkBottomBar;
        [SerializeField] private float blinkCloseDuration = 0.18f;
        [SerializeField] private float blinkHold = 0.1f;
        [SerializeField] private float blinkOpenDuration = 0.35f;

        private bool hasPlayed;
        private bool isPlaying;

        private static VNDirector VN => GameServices.Instance?.VN;

        private void Update()
        {
            if (isPlaying || (triggerOnce && hasPlayed))
            {
                return;
            }

            var vn = VN;
            if (vn == null || string.IsNullOrWhiteSpace(triggerNodeId) || vn.CurrentNodeId != triggerNodeId)
            {
                return;
            }

            hasPlayed = true;
            Play();
        }

        /// <summary>播放整段过场（外部也可以主动调）。</summary>
        public void Play()
        {
            if (isPlaying)
            {
                return;
            }

            PlayAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>直接进最终状态（调试用）。</summary>
        public void RevealTerminalImmediately()
        {
            hasPlayed = true;
            MoveMainUiToFullscreen();
            if (bootRoot != null)
            {
                bootRoot.SetActive(false);
            }

            SetBlinkBars(0f);
        }

        private async UniTaskVoid PlayAsync(CancellationToken ct)
        {
            isPlaying = true;
            try
            {
                LayoutScreenContent();
                await PowerOffAsync(ct);
                BlackoutScreenAsync();
                await BootAsync(ct);
                await OpenTerminalAsync(ct);
                await BlinkAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // 场景销毁，静默。
            }
            finally
            {
                isPlaying = false;
                if (logBeats)
                {
                    Debug.Log("[TerminalBoot] 过场结束。");
                }
            }
        }

        // ---- 图层准备 ----

        /// <summary>
        /// 把 mainUI 挂到内容层下。内容层和 2 是同一个 rect（全屏拉伸、同一个父级），
        /// 所以两者的大小/可见范围完全一致 —— 不需要任何缩放或平移。
        /// </summary>
        private void LayoutScreenContent()
        {
            if (screenContent == null)
            {
                return;
            }

            if (mainUiRoot != null)
            {
                mainUiRoot.SetParent(screenContent, false);
                mainUiRoot.localScale = Vector3.one;
                mainUiRoot.anchoredPosition = Vector2.zero;
            }

            if (logBeats)
            {
                Debug.Log("[TerminalBoot] 内容层与 background/2 同 rect（全屏拉伸），加载与终端都按 2 的大小显示。");
            }
        }

        /// <summary>把 mainUI 变成全屏：放在 background（房间/屏幕层）之上、VN 面板之下。</summary>
        private void MoveMainUiToFullscreen()
        {
            if (mainUiRoot == null)
            {
                return;
            }

            // 关键：不能放到最上层，否则终端会盖住 VN 字幕条和选项。
            // screenContent 的父级就是 background，插在它后面即可。
            var siblingIndex = -1;
            if (screenContent != null && screenContent.parent != null)
            {
                siblingIndex = screenContent.parent.GetSiblingIndex() + 1;
            }

            mainUiRoot.SetParent(canvasRoot != null ? canvasRoot : mainUiRoot.parent, false);
            mainUiRoot.gameObject.SetActive(true);
            mainUiRoot.localScale = Vector3.one;
            mainUiRoot.anchoredPosition = Vector2.zero;

            if (siblingIndex >= 0)
            {
                mainUiRoot.SetSiblingIndex(siblingIndex);
            }
            else
            {
                Debug.LogWarning("[TerminalBoot] 拿不到 background 的层级，mainUI 的排序可能盖住 VN 面板。");
            }
        }

        // ---- 1-2：关机（只塌缩 2，位置不动） ----

        private async UniTask PowerOffAsync(CancellationToken ct)
        {
            Beat("1 关机（只关 background/2，位置不变）");
            if (whiteLineGroup != null)
            {
                whiteLineGroup.alpha = 0f;
            }

            if (newsVisual != null)
            {
                var start = newsVisual.localScale;
                await LerpAsync(0f, 1f, powerOffDuration,
                    t => newsVisual.localScale = new Vector3(
                        Mathf.Lerp(start.x, start.x * powerOffWiden, t),
                        Mathf.Lerp(start.y, 0.02f, t),
                        1f), ct);
            }

            if (whiteLineGroup != null)
            {
                await LerpAsync(0f, 1f, flashDuration, a => whiteLineGroup.alpha = a, ct);
            }

            Beat("2 熄灭");
            if (whiteLine != null)
            {
                await LerpAsync(1f, 0.02f, flashDuration, s => whiteLine.localScale = new Vector3(s, 1f, 1f), ct);
            }

            if (whiteLineGroup != null)
            {
                await LerpAsync(1f, 0f, flashDuration, a => whiteLineGroup.alpha = a, ct);
            }
        }

        // ---- 3：屏幕黑掉（房间照旧可见，所以这里只是隐藏 2） ----

        private void BlackoutScreenAsync()
        {
            Beat("3 屏幕黑掉（隐藏 2，露出深色底）");
            crtFx?.SetFxOff();

            if (newsVisual != null)
            {
                newsVisual.gameObject.SetActive(false);
            }
        }

        // ---- 4：开机自检（在显示器里） ----

        private async UniTask BootAsync(CancellationToken ct)
        {
            Beat("4 开机自检（在屏幕里）");
            if (bootRoot != null)
            {
                bootRoot.SetActive(true);
            }

            if (bootLogText != null)
            {
                var text = new StringBuilder();
                bootLogText.text = string.Empty;
                foreach (var line in bootLines)
                {
                    var content = line ?? string.Empty;
                    foreach (var c in content)
                    {
                        text.Append(c);
                        bootLogText.text = text.ToString();
                        await DelayAsync(bootCharInterval, ct);
                    }

                    text.Append('\n');
                    bootLogText.text = text.ToString();
                    await DelayAsync(bootLineInterval, ct);
                }
            }

            await DelayAsync(bootHoldAfter, ct);
        }

        // ---- 5：打开终端（同样在显示器里） ----

        private async UniTask OpenTerminalAsync(CancellationToken ct)
        {
            Beat("5 打开 mainUI（在屏幕里）");
            if (bootRoot != null)
            {
                bootRoot.SetActive(false);
            }

            if (mainUiRoot != null)
            {
                mainUiRoot.gameObject.SetActive(true);
            }

            await DelayAsync(bootHoldAfter, ct);
        }

        // ---- 6-8：眨眼闭 → 偷换成全屏 → 睁眼 ----

        private async UniTask BlinkAsync(CancellationToken ct)
        {
            var canvasHeight = Mathf.Max(1f, CanvasRect().height);
            var closedHeight = canvasHeight * 0.5f + 4f;

            Beat("6 眨眼闭");
            await LerpAsync(0f, closedHeight, blinkCloseDuration, SetBlinkBars, ct);
            await DelayAsync(blinkHold, ct);

            Beat("7 偷换：mainUI 提到最上层并还原全屏");
            MoveMainUiToFullscreen();

            Beat("8 睁眼 → 全屏终端");
            await LerpAsync(closedHeight, 0f, blinkOpenDuration, SetBlinkBars, ct);
        }

        // ---- 工具 ----

        /// <summary>上下两条眨眼黑边的高度（每条各占这么高）。</summary>
        private void SetBlinkBars(float height)
        {
            if (blinkTopBar != null)
            {
                blinkTopBar.sizeDelta = new Vector2(blinkTopBar.sizeDelta.x, height);
            }

            if (blinkBottomBar != null)
            {
                blinkBottomBar.sizeDelta = new Vector2(blinkBottomBar.sizeDelta.x, height);
            }
        }

        private Rect CanvasRect()
        {
            if (canvasRoot != null)
            {
                return canvasRoot.rect;
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
                return ((RectTransform)root.transform).rect;
            }

            return new Rect(0f, 0f, 1920f, 1080f);
        }

        private void Beat(string label)
        {
            if (logBeats)
            {
                Debug.Log($"[TerminalBoot] {label}");
            }
        }

        private static async UniTask DelayAsync(float seconds, CancellationToken ct)
        {
            if (seconds > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: ct);
            }
            else
            {
                await UniTask.Yield(ct);
            }
        }

        private static async UniTask LerpAsync(float from, float to, float duration, Action<float> apply, CancellationToken ct)
        {
            if (duration <= 0f)
            {
                apply(to);
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                apply(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                await UniTask.Yield(ct);
                elapsed += Time.deltaTime;
            }

            apply(to);
        }
    }
}
