using System;
using TMPro;
using UnityEngine;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 把一段文本变成"本机时间"（左上角侧边栏那个 Time 用的就是这个）。
    ///
    /// 用 <see cref="DateTime.Now"/>（本地时间），每秒刷新一次 —— 不用每帧刷，省一点开销。
    /// 挂在 LeftApp.prefab 里的 Time 物体上，所以**所有用到 LeftApp 的面板**（序幕终端、玩法窗口…）都会显示时间。
    ///
    /// 想在 Inspector 里改格式：`format`（默认 `HH:mm:ss`，也可以写 `HH:mm`），
    /// 需要带日期就打开 `showDate`。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalClockText : MonoBehaviour
    {
        [Tooltip("要写入的文本。留空则用同物体上的 TextMeshProUGUI。")]
        [SerializeField] private TextMeshProUGUI target;

        [Tooltip("时间格式（.NET 格式串），例如 HH:mm:ss / HH:mm。")]
        [SerializeField] private string format = "HH:mm:ss";

        [Tooltip("是否在时间前面加一行日期。")]
        [SerializeField] private bool showDate;

        [Tooltip("日期格式，例如 yyyy-MM-dd / MM月dd日。")]
        [SerializeField] private string dateFormat = "yyyy-MM-dd";

        private float nextRefreshTime;

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponent<TextMeshProUGUI>();
            }

            if (target == null)
            {
                Debug.LogWarning("[Clock] 没有可写入的 TextMeshProUGUI，时间不会显示。", this);
                enabled = false;
                return;
            }

            Refresh(force: true);
        }

        private void OnEnable()
        {
            Refresh(force: true);
        }

        private void Update()
        {
            // 每秒刷一次就够了；unscaledTime 保证暂停/慢放时时间照走。
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            Refresh(force: false);
        }

        private void Refresh(bool force)
        {
            if (target == null)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 1f;

            var now = DateTime.Now; // 本地时间
            var text = now.ToString(string.IsNullOrWhiteSpace(format) ? "HH:mm:ss" : format);
            if (showDate)
            {
                text = $"{now.ToString(string.IsNullOrWhiteSpace(dateFormat) ? "yyyy-MM-dd" : dateFormat)}\n{text}";
            }

            if (force || target.text != text)
            {
                target.text = text;
            }
        }
    }
}
