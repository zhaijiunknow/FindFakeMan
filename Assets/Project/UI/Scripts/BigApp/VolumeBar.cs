using System;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 一条音量条 —— 现在它是 **`Slider` ↔ `AudioManager` 的适配器** ✓。
    ///
    /// 为什么改成这样 ✗→✓：以前这里自己算位置（填充图的 `anchorMax.x` = 比例 ✓），
    /// 好处是不依赖任何子层级 ✓，但**没有滑块圆点**✗、也不参与 EventSystem 导航（键盘/手柄选不到 ✗）。
    /// 现在控件由建造工具搭成标准 `Slider` 层级 ✓（`Fill Area/Fill` + `Handle Slide Area/Handle` ✓），
    /// 圆点用美术给的 `slide.png` ✓ —— 于是"点在轨道任意处就跳过去"和"拖圆点"两种操作都由 `Slider` 自带 ✓。
    ///
    /// 这一层只保留"对外接口不变" ✓：`ValueChanged` 事件 + `SetValue` ✓ ——
    /// 所以 <see cref="SettingsPageView.HookAudio"/> 里那套订阅**一个字都不用改** ✓。
    /// </summary>
    public sealed class VolumeBar : MonoBehaviour
    {
        [Tooltip("真正干活的 Slider ✓（建造工具搭好层级后，由 Configure 传进来 ✓）。")]
        [SerializeField] private Slider slider;

        [Tooltip("当前值 0~1 ✓（Slider 缺席时的兜底存储 ✓）。")]
        [SerializeField, Range(0f, 1f)] private float value = 0.6f;

        [Tooltip("Start 时是否把当前值推给监听者一次 ✓（让 AudioManager 从一开始就跟 UI 一致 ✓）。")]
        [SerializeField] private bool applyOnStart = true;

        /// <summary>值变化（拖动时连续触发）✓。</summary>
        public event Action<float> ValueChanged;

        /// <summary>当前值（0~1）✓。</summary>
        public float Value => slider != null ? slider.value : value;

        private void Awake()
        {
            HookSlider();
        }

        private void Start()
        {
            if (applyOnStart)
            {
                ValueChanged?.Invoke(Value);
            }
        }

        /// <summary>运行时装配（视图自建控件时调 ✓）：把正在干活的 Slider 接过来 ✓。</summary>
        public void Configure(Slider target, float initial, bool notify = false)
        {
            slider = target;
            value = Mathf.Clamp01(initial);

            HookSlider();
            SetValue(value, false);

            if (notify)
            {
                ValueChanged?.Invoke(value);
            }
        }

        /// <summary>外部设值（比如开页面时把 AudioManager 的真实音量灌进来 ✓）。</summary>
        public void SetValue(float newValue, bool notify = true)
        {
            value = Mathf.Clamp01(newValue);

            if (slider != null)
            {
                // 用 WithoutNotify ✓：不然会绕回来触发一次 OnSliderChanged ✓，等于同一次改动报两遍 ✗。
                slider.SetValueWithoutNotify(value);
            }

            if (notify)
            {
                ValueChanged?.Invoke(value);
            }
        }

        /// <summary>接 Slider 的回调（幂等 ✓；用固定方法而不是 lambda ✓ —— lambda 没法 RemoveListener ✗）。</summary>
        private void HookSlider()
        {
            if (slider == null)
            {
                return;
            }

            slider.onValueChanged.RemoveListener(OnSliderChanged);
            slider.onValueChanged.AddListener(OnSliderChanged);
        }

        private void OnSliderChanged(float newValue)
        {
            value = Mathf.Clamp01(newValue);
            ValueChanged?.Invoke(value);
        }
    }
}
