using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Narrative.Scripts;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
    /// <summary>
    /// 序章演出导演：黑屏切入 → 淡出露背景"2"(新闻联播图) → 等几句对白 → CRT 扫描线特效
    /// → 背景 scale 拉远(伪镜头) → 黑屏渐入 → 跳帧(scale 突变更远) → 黑屏渐出。
    /// 与 VNDirector 对白并行；全程只动 UI(正交相机不渲染 UI)，不碰相机。
    /// </summary>
    public sealed class ProloguePerformanceDirector : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private CanvasGroup blackOverlay;      // 全屏黑(黑屏)
        [SerializeField] private Image backgroundImage;         // 背景"2"(新闻联播图)
        [SerializeField] private ScreenFxOverlay screenFx;      // CRT 扫描线层

        [Header("参数")]
        [Tooltip("黑屏→露背景的淡出时长")] [SerializeField] private float fadeInDuration = 1.2f;
        [Tooltip("播放几句对白的等待时间")] [SerializeField] private float dialogueWaitSeconds = 4f;
        [Tooltip("CRT 特效显示时长")] [SerializeField] private float fxDuration = 2f;
        [Tooltip("第一段拉远终点(近)")] [SerializeField] private float nearScale = 0.6f;
        [Tooltip("黑屏跳变后(更远)")] [SerializeField] private float farScale = 0.3f;
        [Tooltip("继续拉远的终点(远)")] [SerializeField] private float finalScale = 0.15f;
        [Tooltip("第一段拉远时长")] [SerializeField] private float pullDuration = 2.5f;
        [Tooltip("黑屏跳变后继续拉远时长")] [SerializeField] private float pullSecond = 1.5f;
        [Tooltip("黑屏闪黑的渐入/渐出时长(短)")] [SerializeField] private float blackFadeDuration = 0.2f;

        private void Start()
        {
            RunAsync(destroyCancellationToken).Forget();
        }

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            try
            {
                // 1. 黑屏切入：默认 alpha=1(黑屏)，淡出露背景"2"。
                if (blackOverlay != null) blackOverlay.alpha = 1f;
                if (backgroundImage != null) backgroundImage.gameObject.SetActive(true);
                await FadeOverlayAsync(blackOverlay, 0f, fadeInDuration, ct);
                Debug.Log("[PrologueDirector] 黑屏淡出，露背景2。");

                // 2. 播放几句对白（VNDirector 并行播 opening_news）。
                await UniTask.Delay(TimeSpan.FromSeconds(dialogueWaitSeconds), cancellationToken: ct);
                Debug.Log("[PrologueDirector] 对白段落，准备 CRT。");

                // 3. CRT 扫描线特效。
                if (screenFx != null)
                {
                    screenFx.SetFxOn();
                    screenFx.PlayFlicker();
                }
                Debug.Log("[PrologueDirector] CRT 扫描线特效显示。");
                await UniTask.Delay(TimeSpan.FromSeconds(fxDuration), cancellationToken: ct);

                // 4+5. 第一段连续拉远（运动不中断）→ 黑屏最黑时强制跳变到更远 → 渐出后继续拉远。
                var zoomTarget = ZoomTarget();
                if (zoomTarget != null)
                {
                    zoomTarget.localScale = Vector3.one; // 复位
                    using var zoomCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    var zoom1 = ScaleToAsync(zoomTarget, nearScale, pullDuration, zoomCts.Token); // 第一段连续拉远（黑屏前一直跑）
                    // 黑屏固定在段尾：第一段拉远最后 blackFade 秒时黑屏渐入（与之重叠，避免硬停）。
                    await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, pullDuration - blackFadeDuration)), cancellationToken: ct);
                    await FadeOverlayAsync(blackOverlay, 1f, blackFadeDuration, ct);   // 黑屏渐入（拉远仍在后台跑）

                    zoomCts.Cancel();                                                // 停掉第一段
                    zoomTarget.localScale = Vector3.one * farScale;                  // 黑屏最黑：瞬间跳变到更远
                    try { await zoom1; } catch (OperationCanceledException) { }

                    if (screenFx != null) screenFx.SetFxOff();
                    // 渐出与拉远并行：0.3 → 0.15 在渐出那刻仍在进行，渐出后镜头继续拉远（伪造运动持续、无断裂）。
                    var zoom2 = ScaleToAsync(zoomTarget, finalScale, pullSecond, ct);
                    await FadeOverlayAsync(blackOverlay, 0f, blackFadeDuration, ct);
                    await zoom2;
                }
                Debug.Log($"[PrologueDirector] 跳变后继续拉远到 {finalScale}，完成。");

                Debug.Log("[PrologueDirector] 序章演出完成。");
            }
            catch (OperationCanceledException)
            {
                // 场景销毁取消，静默。
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PrologueDirector] {ex}");
            }
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

        /// <summary>缩放目标是 background 整个容器（而非仅"2"这张图）：取 background/2 的父级。</summary>
        private RectTransform ZoomTarget()
        {
            if (backgroundImage == null) return null;
            var parent = backgroundImage.rectTransform.parent as RectTransform;
            return parent != null ? parent : backgroundImage.rectTransform;
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
