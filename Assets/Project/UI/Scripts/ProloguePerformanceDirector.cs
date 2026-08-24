using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Managers;
using Project.Narrative.Scripts;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
    /// <summary>
    /// 序章演出导演：视觉与 VNDirector **节点进度绑定**（不是固定延时）。
    ///  - 黑屏切入淡出露背景"2"(新闻联播图)，对白由 VNDirector 播放。
    ///  - 播到 crtStartNodeId → 开启 CRT 扫描线。
    ///  - 播到 zoomStartNodeId → 开始第一段拉远 → 黑屏闪黑（CRT 在此结束）→ 跳变 → 渐出 → 第二段拉远。
    /// 全程只动 UI（正交相机不渲染 UI），不碰相机。
    /// </summary>
    public sealed class ProloguePerformanceDirector : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private CanvasGroup blackOverlay;      // 全屏黑(黑屏)
        [SerializeField] private Image backgroundImage;         // 背景"2"(新闻联播图)
        [SerializeField] private ScreenFxOverlay screenFx;      // CRT 扫描线层

        [Header("节点触发")]
        [Tooltip("播到这个节点开始 CRT")] [SerializeField] private string crtStartNodeId = "opening_news_003";
        [Tooltip("播到这个节点开始拉远")] [SerializeField] private string zoomStartNodeId = "opening_news_004";

        [Header("参数")]
        [Tooltip("黑屏→露背景的淡出时长")] [SerializeField] private float fadeInDuration = 1.2f;
        [Tooltip("第一段拉远终点(近)")] [SerializeField] private float nearScale = 0.6f;
        [Tooltip("黑屏跳变后(更远)")] [SerializeField] private float farScale = 0.3f;
        [Tooltip("继续拉远的终点(远)")] [SerializeField] private float finalScale = 0.15f;
        [Tooltip("第一段拉远时长")] [SerializeField] private float pullDuration = 2.5f;
        [Tooltip("黑屏跳变后继续拉远时长")] [SerializeField] private float pullSecond = 1.5f;
        [Tooltip("黑屏闪黑的渐入/渐出时长(短)")] [SerializeField] private float blackFadeDuration = 0.2f;

        private bool crtOn;
        private bool zoomStarted;

        private void Start()
        {
            RunIntroAsync(destroyCancellationToken).Forget();
            StartStoryAsync(destroyCancellationToken).Forget();
        }

        private void Update()
        {
            var vn = VN;
            if (vn == null) return;
            var nodeId = vn.CurrentNodeId;

            if (!crtOn && nodeId == crtStartNodeId)
            {
                crtOn = true;
                if (screenFx != null) screenFx.SetFxOn();
            }

            if (!zoomStarted && nodeId == zoomStartNodeId)
            {
                zoomStarted = true;
                RunZoomSequenceAsync(destroyCancellationToken).Forget();
            }
        }

        /// <summary>黑屏切入 → 淡出露背景"2"（对白由 VNDirector 并行播放）。</summary>
        private async UniTaskVoid RunIntroAsync(CancellationToken ct)
        {
            if (blackOverlay != null) blackOverlay.alpha = 1f;
            if (backgroundImage != null) backgroundImage.gameObject.SetActive(true);
            await FadeOverlayAsync(blackOverlay, 0f, fadeInDuration, ct);
            Debug.Log("[PrologueDirector] 黑屏淡出，露背景2；对白按节点播放。");
        }

        /// <summary>通过全局单例 VNDirector 播放序章内容（chapter_prologue_story）。</summary>
        private async UniTaskVoid StartStoryAsync(CancellationToken ct)
        {
            var vn = VN;
            var chapter = LoadChapter("chapter_prologue_story");
            Debug.Log($"[PrologueDirector] StartStory: vn={(vn != null)} chapter={(chapter != null)} chapterId={(chapter != null ? chapter.ChapterId : "-")} isPlaying={(vn != null && vn.IsPlaying)}");
            if (vn != null && chapter != null && !vn.IsPlaying)
            {
                await vn.StartChapter(chapter);
                Debug.Log($"[PrologueDirector] StartChapter 结束 currentNode={vn.CurrentNodeId}");
            }
            else if (vn != null && chapter != null)
            {
                Debug.Log("[PrologueDirector] VN 已在播放，跳过 StartChapter");
            }
            else
            {
                Debug.LogWarning("[PrologueDirector] 未找到全局 VNDirector 或序章章节，无法播放对白。");
            }
        }

        private static VNChapterConfig LoadChapter(string chapterId) =>
            Resources.LoadAll<VNChapterConfig>(string.Empty)
                .FirstOrDefault(c => c != null && c.ChapterId == chapterId);

        /// <summary>第一段拉远 → 黑屏闪黑(CRT 结束) → 跳变 → 渐出 → 第二段拉远。</summary>
        private async UniTaskVoid RunZoomSequenceAsync(CancellationToken ct)
        {
            try
            {
                var zoomTarget = ZoomTarget();
                if (zoomTarget == null) return;

                zoomTarget.localScale = Vector3.one; // 复位
                using var zoomCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var zoom1 = ScaleToAsync(zoomTarget, nearScale, pullDuration, zoomCts.Token);
                // 黑屏固定在段尾：与第一段拉远最后 blackFade 秒重叠，避免硬停。
                await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, pullDuration - blackFadeDuration)), cancellationToken: ct);
                await FadeOverlayAsync(blackOverlay, 1f, blackFadeDuration, ct);

                zoomCts.Cancel();                                       // 停掉第一段
                zoomTarget.localScale = Vector3.one * farScale;        // 黑屏最黑：瞬间跳变到更远
                try { await zoom1; } catch (OperationCanceledException) { }

                if (screenFx != null) screenFx.SetFxOff();             // CRT 在闪黑时结束
                // 渐出与第二段拉远并行：渐出那刻镜头仍在后拉（伪造运动持续）。
                var zoom2 = ScaleToAsync(zoomTarget, finalScale, pullSecond, ct);
                await FadeOverlayAsync(blackOverlay, 0f, blackFadeDuration, ct);
                await zoom2;
                Debug.Log($"[PrologueDirector] 拉远完成到 {finalScale}。");
            }
            catch (OperationCanceledException)
            {
                // 场景销毁取消，静默。
            }
        }

        /// <summary>全局单例 VNDirector（经 GameServices 快捷访问）。</summary>
        private VNDirector VN => GameServices.Instance?.VN;

        /// <summary>缩放目标是 background 整个容器（而非仅"2"这张图）：取 background/2 的父级。</summary>
        private RectTransform ZoomTarget()
        {
            if (backgroundImage == null) return null;
            var parent = backgroundImage.rectTransform.parent as RectTransform;
            return parent != null ? parent : backgroundImage.rectTransform;
        }

        private static async UniTask FadeOverlayAsync(CanvasGroup overlay, float target, float duration, CancellationToken ct)
        {
            if (overlay == null) return;
            var start = overlay.alpha;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                overlay.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                await UniTask.Yield(ct);
                elapsed += Time.deltaTime;
            }

            overlay.alpha = target;
        }

        private static async UniTask ScaleToAsync(RectTransform rect, float scale, float duration, CancellationToken ct)
        {
            if (rect == null) return;
            var start = rect.localScale;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                float t = Mathf.Clamp01(elapsed / duration);
                rect.localScale = Vector3.one * Mathf.Lerp(start.x, scale, t); // 匀速：后段也持续后拉，黑屏前不停
                await UniTask.Yield(ct);
                elapsed += Time.deltaTime;
            }

            rect.localScale = Vector3.one * scale;
        }
    }
}
