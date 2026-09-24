using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 一条音量条：点一下、按住拖都改值。
    ///
    /// 为什么不用 uGUI 的 <see cref="Slider"/>：参考图里那种横条**没有滑块圆点**，
    /// 就是一个"填到某个比例"的底 + 填充 ✗→✓；而 Slider 要按它的规矩搭 Background/FillArea/HandleArea/Handle
    /// 四层结构（预制体里没有现成的 ✗，运行时搭容易歪 ✗）。
    /// 这里直接用锚点：填充图的 `anchorMax.x` 就是比例 ✓，天然跟着窗口缩放 ✓。
    ///
    /// 用法：挂在"整条轨道"那个 Image 上（它负责吃点击 ✓），fill 传它的填充子图 ✓。
    /// </summary>
    public sealed class VolumeBar : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [SerializeField] private RectTransform track;
        [SerializeField] private Image fill;
        [SerializeField, Range(0f, 1f)] private float value = 0.6f;
        [SerializeField] private bool applyOnStart = true;

        /// <summary>值变化（拖的时候会连续触发）。</summary>
        public event Action<float> ValueChanged;

        /// <summary>当前值（0~1）。</summary>
        public float Value => value;

        private void Start()
        {
            Apply();
            if (applyOnStart)
            {
                ValueChanged?.Invoke(value);
            }
        }

        /// <summary>运行时装配（建造工具/视图自建时用）。</summary>
        public void Configure(RectTransform trackRect, Image fillImage, float initial, bool notify = false)
        {
            track = trackRect != null ? trackRect : (RectTransform)transform;
            fill = fillImage;
            value = Mathf.Clamp01(initial);
            Apply();

            if (notify)
            {
                ValueChanged?.Invoke(value);
            }
        }

        public void SetValue(float newValue, bool notify = true)
        {
            value = Mathf.Clamp01(newValue);
            Apply();

            if (notify)
            {
                ValueChanged?.Invoke(value);
            }
        }

        public void OnPointerDown(PointerEventData eventData) => ApplyFromPointer(eventData);

        public void OnDrag(PointerEventData eventData) => ApplyFromPointer(eventData);

        private void ApplyFromPointer(PointerEventData eventData)
        {
            if (track == null)
            {
                track = (RectTransform)transform;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position, eventData.pressEventCamera, out var local))
            {
                return;
            }

            // local 是相对 pivot 的坐标；rect.xMin/xMax 同一空间，所以直接插值即可 ✓。
            var t = Mathf.InverseLerp(track.rect.xMin, track.rect.xMax, local.x);
            SetValue(t);
        }

        private void Apply()
        {
            if (fill != null)
            {
                // 填充图用拉伸锚点（min 0 → max 1），把 max.x 设成比例即可 ✓。
                fill.rectTransform.anchorMin = new Vector2(0f, 0f);
                fill.rectTransform.anchorMax = new Vector2(value, 1f);
                fill.rectTransform.offsetMin = Vector2.zero;
                fill.rectTransform.offsetMax = Vector2.zero;
            }
        }
    }
}
