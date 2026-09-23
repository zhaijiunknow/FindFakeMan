using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.UI.Scripts
{
    /// <summary>
    /// 选项按钮的表现层：悬停 / 选中时把柔光描边淡入。
    /// 挂在 VNChoiceButton 预制体根节点上，柔光由子物体 Glow（CanvasGroup）承载。
    ///
    /// 沿用项目既有的视觉语义：普通态是深色板 + 细边，被指向/被选中时亮起描边
    /// （同 小软件/select_button.png 的用法）。
    /// </summary>
    public sealed class VnChoiceButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private CanvasGroup glowGroup;
        [Tooltip("柔光淡入淡出时长（秒，不受 TimeScale 影响）。")]
        [SerializeField] private float fadeDuration = 0.12f;
        [Tooltip("高亮时的柔光强度（CanvasGroup.alpha）。")]
        [SerializeField] private float highlightAlpha = 1f;

        private float targetAlpha;
        private bool isFading;

        /// <summary>键盘/手柄导航或演出脚本可以直接驱动高亮。</summary>
        public void SetHighlighted(bool highlighted)
        {
            targetAlpha = highlighted ? highlightAlpha : 0f;
            if (glowGroup == null)
            {
                return;
            }

            if (!isFading)
            {
                FadeLoop().Forget();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHighlighted(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHighlighted(false);
        }

        private void Awake()
        {
            if (glowGroup != null)
            {
                glowGroup.alpha = 0f;
            }
        }

        /// <summary>
        /// 单条常驻淡入淡出循环：中途改目标只换 targetAlpha，不会产生互相打架的多条动画。
        /// </summary>
        private async UniTaskVoid FadeLoop()
        {
            isFading = true;
            try
            {
                while (glowGroup != null && !Mathf.Approximately(glowGroup.alpha, targetAlpha))
                {
                    var step = fadeDuration > 0f ? Time.unscaledDeltaTime / fadeDuration : 1f;
                    glowGroup.alpha = Mathf.MoveTowards(glowGroup.alpha, targetAlpha, step);
                    await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
                }
            }
            catch (System.OperationCanceledException)
            {
                // 对象销毁时取消，正常路径。
            }
            finally
            {
                isFading = false;
            }
        }
    }
}
