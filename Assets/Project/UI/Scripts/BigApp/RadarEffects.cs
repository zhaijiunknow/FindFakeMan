using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 雷达（探测器）的动态效果：常驻慢速扫描 → 拖工具时加速 → 目标有效时闪一下。
    ///
    /// 做法：一张"扇形渐变"贴图（<c>RadarSweep.png</c>，构建工具代码生成），
    /// 放在探测器圆盘里面（圆盘上本来就有 Mask，所以扇形会被裁进圆盘范围，正好是雷达的观感），
    /// 每帧绕中心旋转。由 <see cref="InvestigationHudView"/> 在拖拽开始/结束、判定有效时驱动。
    /// </summary>
    public sealed class RadarEffects : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("扇形贴图（贴图本身朝上、向右张开）。")]
        [SerializeField] private Image sweep;

        [Header("扫描")]
        [Tooltip("常驻扫描速度（度/秒）。")]
        [SerializeField] private float sweepSpeed = 55f;
        [Tooltip("拖工具时扫描速度倍数。")]
        [SerializeField] private float scanningMultiplier = 3f;
        [Tooltip("关闭后不扫描（只保留 Ping 反馈）。")]
        [SerializeField] private bool alwaysSweep = true;

        [Header("颜色")]
        [SerializeField] private Color idleColor = new Color(0.55f, 0.85f, 1f, 0.30f);
        [SerializeField] private Color scanningColor = new Color(0.60f, 0.90f, 1f, 0.55f);
        [SerializeField] private Color pingColor = new Color(0.75f, 1f, 0.85f, 1f);
        [Tooltip("Ping 淡出时长（秒）。")]
        [SerializeField] private float pingDuration = 0.45f;

        private float angle;
        private bool scanning;
        private float pingTimer;

        /// <summary>
        /// 当前扫描角度（度）：0 = 正上方，顺时针增大。
        /// 手势表盘靠它把进度弧转到"雷达正扫到的位置"，这样进度和扫描看起来是同一件事。
        /// </summary>
        public float SweepAngle => angle;

        /// <summary>是否正在加速扫描（拖工具中）。</summary>
        public bool IsScanning => scanning;

        /// <summary>拖工具中 = 加速扫描。</summary>
        public void SetScanning(bool value)
        {
            scanning = value;
        }

        /// <summary>闪一下（判定工具有效、或工具成功作用时调用）。</summary>
        public void Ping()
        {
            pingTimer = Mathf.Max(0.01f, pingDuration);
        }

        private void Update()
        {
            var speed = sweepSpeed * (scanning ? scanningMultiplier : 1f);
            if (alwaysSweep && sweep != null)
            {
                // localEulerAngles.z 取负 = 顺时针。
                angle = Mathf.Repeat(angle + speed * Time.unscaledDeltaTime, 360f);
                sweep.rectTransform.localEulerAngles = new Vector3(0f, 0f, -angle);
            }

            if (sweep == null)
            {
                return;
            }

            var baseColor = scanning ? scanningColor : idleColor;
            if (pingTimer > 0f)
            {
                pingTimer -= Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(pingTimer / Mathf.Max(0.01f, pingDuration));
                sweep.color = Color.Lerp(baseColor, pingColor, t);
            }
            else
            {
                sweep.color = baseColor;
            }
        }
    }
}
