using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Narrative.Scripts;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
    /// <summary>
    /// 序章开场演出：视觉与 VNDirector **节点进度绑定**（不是固定延时）。
    ///  - 开场：黑屏切入淡出，露出 background/2（新闻联播画面）；
    ///  - 播到 crtStartNodeId → 开启 CRT 扫描线；
    ///  - 播到 pullStartNodeId → **镜头后退**：整个 background 从 startScale(1.4，书桌/显示器特写)
    ///    平滑缩到 endScale(0.4，退到远景、四周留黑)；**推远结束后**再把房间由亮版切到夜版；
    ///    最后关掉 CRT。
    ///
    /// 房间层：只有一层房间图，默认是亮版（扣a3）；推远结束时**直接把它的 sprite 换成夜版（扣a1）**，
    /// 不做 alpha 淡入 —— 也就是"看完新闻、退开，房间的灯沉下去"。
    ///
    /// 全程只动 UI（正交相机不渲染 UI），不碰相机。
    /// </summary>
    public sealed class ProloguePerformanceDirector : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private CanvasGroup blackOverlay;   // 全屏黑（开场淡入）
        [SerializeField] private Image backgroundImage;      // background/2（新闻画面）
        [SerializeField] private ScreenFxOverlay screenFx;   // CRT 扫描线层
        [Tooltip("房间图：推远结束时把它的 sprite 直接换成下面的 nightSprite（夜版）。")]
        [SerializeField] private Image nightRoom;
        [Tooltip("夜版房间贴图（扣a1）：推远结束时赋给上面的房间图。")]
        [SerializeField] private Sprite nightSprite;
        [Tooltip("可选：直接引用 VNDirector；留空则走 GameServices 全局单例。")]
        [SerializeField] private VNDirector vndirector;

        [Header("节点触发")]
        [Tooltip("播到这个节点开始 CRT")] [SerializeField] private string crtStartNodeId = "opening_news_003";
        [Tooltip("播到这个节点开始镜头后退（opening_news_004）")] [SerializeField] private string pullStartNodeId = "opening_news_004";

        [Header("开场")]
        [Tooltip("黑屏→露画面的淡出时长（只有单独运行本场景时用；走场景转场进来时由 CRT 开机接管，这里跳过）")]
        [SerializeField] private float fadeInDuration = 0.4f;

        [Header("推远（004）")]
        [Tooltip("开场时画面的放大倍数：1.4 = 只看得到房间中段（书桌/显示器特写）")]
        [SerializeField] private float startScale = 1.4f;
        [Tooltip("推远结束时的缩放：0.4 = 退到远景，四周留黑")]
        [SerializeField] private float endScale = 0.4f;
        [SerializeField] private float pullDuration = 2f;
        [Tooltip("切完房间后是否关掉 CRT 扫描线")]
        [SerializeField] private bool stopCrtAfterPull = true;

        private bool crtOn;
        private bool pullStarted;

        private VNDirector VN => vndirector != null ? vndirector : GameServices.Instance?.VN;

        private void Start()
        {
            // 开场就贴到"特写"状态；此时全屏黑还盖着，所以这次跳变看不见。
            ApplyStartScale();
            RunIntroAsync(destroyCancellationToken).Forget();
            StartStoryAsync(destroyCancellationToken).Forget();
        }

        private void Update()
        {
            var vn = VN;
            if (vn == null)
            {
                return;
            }

            var nodeId = vn.CurrentNodeId;

            if (!crtOn && nodeId == crtStartNodeId)
            {
                crtOn = true;
                if (screenFx != null)
                {
                    screenFx.SetFxOn();
                }
            }

            if (!pullStarted && nodeId == pullStartNodeId)
            {
                pullStarted = true;
                RunPullBackAsync(destroyCancellationToken).Forget();
            }
        }

        /// <summary>黑屏切入 → 淡出露画面（对白由 VNDirector 并行播放）。</summary>
        private async UniTaskVoid RunIntroAsync(CancellationToken ct)
        {
            if (blackOverlay != null)
            {
                blackOverlay.alpha = 1f;
            }

            if (backgroundImage != null)
            {
                backgroundImage.gameObject.SetActive(true);
            }

            // 如果是走场景流转进来的，画面已经被 ScreenTransition 的「CRT 开机」盖着了，
            // 这里就不再自己淡入一次（否则"展开完又渐入一次黑"会变成两层黑，很难看）。
            // 直接在编辑器里单独跑本场景时 IsBlack 为 false，照常淡入。
            var duration = ScreenTransition.IsBlack ? 0f : fadeInDuration;
            await FadeOverlayAsync(blackOverlay, 0f, duration, ct);
            Debug.Log($"[PrologueDirector] 黑屏淡出（{duration:0.##}s）；对白按节点播放。");
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
                Debug.LogWarning("[PrologueDirector] 未找到 VNDirector 或序章章节，无法播放对白。");
            }
        }

        private static VNChapterConfig LoadChapter(string chapterId) =>
            Resources.LoadAll<VNChapterConfig>(string.Empty)
                .FirstOrDefault(c => c != null && c.ChapterId == chapterId);

        private void ApplyStartScale()
        {
            var target = ZoomTarget();
            if (target != null)
            {
                target.localScale = Vector3.one * Mathf.Max(0.01f, startScale);
            }
        }

        /// <summary>镜头后退 → 房间切夜版 → 关 CRT，顺序执行。</summary>
        private async UniTaskVoid RunPullBackAsync(CancellationToken ct)
        {
            try
            {
                var target = ZoomTarget();
                if (target == null)
                {
                    return;
                }

                Debug.Log($"[PrologueDirector] 004：镜头后退 {target.localScale.x:0.##} → {endScale:0.##}");
                await ScaleToAsync(target, endScale, pullDuration, ct);

                Debug.Log("[PrologueDirector] 推远结束，房间切到夜版。");
                SwitchRoomToNight();

                if (stopCrtAfterPull && screenFx != null)
                {
                    screenFx.SetFxOff();
                }

                Debug.Log("[PrologueDirector] 拉远完成。");
            }
            catch (OperationCanceledException)
            {
                // 场景销毁取消，静默。
            }
        }

        /// <summary>推远结束后：把房间图的 sprite 直接换成夜版（硬切，不做 alpha）。</summary>
        private void SwitchRoomToNight()
        {
            if (nightRoom == null)
            {
                Debug.LogWarning("[PrologueDirector] 没接房间图（nightRoom），跳过切夜版。");
                return;
            }

            if (nightSprite == null)
            {
                Debug.LogWarning("[PrologueDirector] 没接夜版贴图（nightSprite），跳过切夜版。");
                return;
            }

            if (!nightRoom.gameObject.activeSelf)
            {
                nightRoom.gameObject.SetActive(true);
            }

            nightRoom.enabled = true;
            nightRoom.sprite = nightSprite;
            // 顺手把颜色还原成不透明，免得之前被调过 alpha。
            nightRoom.color = new Color(nightRoom.color.r, nightRoom.color.g, nightRoom.color.b, 1f);
            Debug.Log($"[PrologueDirector] 房间已切到夜版：{nightSprite.name}（对象 {nightRoom.name}）");
        }

        /// <summary>缩放目标是 background 整个容器（而非仅"2"这张图）：取"2"的父级。</summary>
        private RectTransform ZoomTarget()
        {
            if (backgroundImage == null)
            {
                return null;
            }

            var parent = backgroundImage.rectTransform.parent as RectTransform;
            return parent != null ? parent : backgroundImage.rectTransform;
        }

        private static async UniTask FadeOverlayAsync(CanvasGroup overlay, float target, float duration, CancellationToken ct)
        {
            if (overlay == null)
            {
                return;
            }

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

        /// <summary>镜头后退用先快后慢（ease-out），停下时更稳。</summary>
        private static async UniTask ScaleToAsync(RectTransform rect, float scale, float duration, CancellationToken ct)
        {
            if (rect == null)
            {
                return;
            }

            var start = rect.localScale.x;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                var t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                rect.localScale = Vector3.one * Mathf.Lerp(start, scale, eased);
                await UniTask.Yield(ct);
                elapsed += Time.deltaTime;
            }

            rect.localScale = Vector3.one * scale;
        }
    }
}
