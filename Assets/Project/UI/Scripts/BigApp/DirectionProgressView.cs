using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 方向手势的进度可视化（底特律式）：一个逐渐填满的**圆环** + 四个方向箭头（当前方向那个会亮起来并随进度放大）。
    ///
    /// 由 <see cref="InspectorDragHandler"/> 驱动：
    ///  - 开始往某个方向拖 → 显示，对应方向的箭头点亮；
    ///  - 每帧把进度传进 <see cref="SetProgress"/>，圆环从正上方顺时针填充；
    ///  - 没填满松手 → 圆环停在当前进度并缓慢回退（看得见）；
    ///  - 退到 0 → 整组隐藏。
    ///
    /// 贴图由构建工具用代码生成（`Assets/Project/Resource/UI/大软件/DetroitRing.png` 与 `DetroitArrow.png`），
    /// 所以不需要美术另外出图。
    /// </summary>
    public sealed class DirectionProgressView : MonoBehaviour
    {
        /// <summary>表盘上的四个方向。</summary>
        public enum MeterDirection
        {
            None = 0,
            Up = 1,
            Down = 2,
            Left = 3,
            Right = 4,
        }

        [Header("引用")]
        [Tooltip("整个表盘（平时关闭，拖拽时才打开）。留空则用自己这个物体。")]
        [SerializeField] private GameObject root;
        [Tooltip("圆环填充：需要有 sprite，脚本会把它设成 Filled / Radial360 / 从正上方顺时针。")]
        [SerializeField] private Image ringFill;
        [Tooltip("四个方向箭头，顺序固定为：上、下、左、右。")]
        [SerializeField] private Image[] directionArrows = new Image[4];
        [Tooltip("环中央显示这个方向对应的动作名。")]
        [SerializeField] private TextMeshProUGUI actionText;
        [Tooltip("雷达（探测器）动效：进度弧会跟着它的扫描角度转 —— 让『进度』和『扫描』看起来是同一件事。")]
        [SerializeField] private RadarEffects radar;

        [Header("表现")]
        [Tooltip("圆环填充色；越满越亮。")]
        [SerializeField] private Color fillColor = new Color(0.42f, 0.66f, 1f, 1f);
        [SerializeField] private Color fillDimColor = new Color(0.42f, 0.66f, 1f, 0.30f);
        [Tooltip("没被选中的箭头颜色。")]
        [SerializeField] private Color arrowIdleColor = new Color(1f, 1f, 1f, 0.20f);
        [Tooltip("当前方向的箭头颜色。")]
        [SerializeField] private Color arrowActiveColor = new Color(1f, 1f, 1f, 1f);
        [Tooltip("进度填满时，当前方向箭头放大的倍数。")]
        [SerializeField] private float arrowActiveScale = 1.35f;

        [Header("方向可用性（由 InspectorDragHandler 推过来）")]
        [Tooltip("可用但还没被扇形扫到时的箭头颜色。")]
        [SerializeField] private Color arrowAvailableColor = new Color(1f, 1f, 1f, 0.55f);
        [Tooltip("扇形扫到这个方向（且它可用）时的颜色 —— 就是那一下『叮』的高亮。")]
        [SerializeField] private Color arrowSweptColor = new Color(0.75f, 1f, 0.9f, 1f);
        [Tooltip("扇形扫到判定：方向角与扫描角相差在这个范围内算扫到（度）。")]
        [SerializeField] private float sweepHitTolerance = 14f;

        // 顺序与 directionArrows 一致：上(0°) 下(180°) 左(270°) 右(90°)
        private readonly bool[] directionAvailable = new bool[4];

        /// <summary>四个方向现在能不能做（上/下/左/右），由 InspectorDragHandler 在选中物品时算好推过来。</summary>
        public void SetAvailableDirections(bool[] available)
        {
            for (var i = 0; i < directionAvailable.Length; i++)
            {
                directionAvailable[i] = available != null && i < available.Length && available[i];
            }
        }

        /// <summary>
        /// 每帧按雷达扫描角刷新箭头颜色：
        /// 不可用 = 很暗；可用没扫到 = 中等亮；可用且被扫到 = 高亮变色。
        /// </summary>
        private void Update()
        {
            if (radar == null || directionArrows == null || !Target.activeSelf)
            {
                return;
            }

            for (var i = 0; i < directionArrows.Length; i++)
            {
                var arrow = directionArrows[i];
                if (arrow == null)
                {
                    continue;
                }

                // 正在拖拽的那个方向交给 Show/SetProgress 管（点亮 + 放大），这里不抢。
                if (currentDirection != MeterDirection.None && i == (int)currentDirection - 1)
                {
                    continue;
                }

                var available = directionAvailable[i];
                var swept = available && Mathf.Abs(Mathf.DeltaAngle(radar.SweepAngle, DirectionAngle(i))) <= sweepHitTolerance;
                arrow.color = !available ? arrowIdleColor : swept ? arrowSweptColor : arrowAvailableColor;
            }
        }

        private static float DirectionAngle(int index)
        {
            switch (index)
            {
                case 0: return 0f;     // 上
                case 1: return 180f;   // 下
                case 2: return 270f;   // 左
                default: return 90f;   // 右
            }
        }

        private MeterDirection currentDirection = MeterDirection.None;

        private GameObject Target => root != null ? root : gameObject;

        private void Awake()
        {
            if (ringFill != null)
            {
                ringFill.type = Image.Type.Filled;
                ringFill.fillMethod = Image.FillMethod.Radial360;
                ringFill.fillOrigin = (int)Image.Origin360.Top;
                ringFill.fillClockwise = true;
                ringFill.fillAmount = 0f;
                ringFill.color = fillDimColor;
            }

            Hide();
        }

        /// <summary>开始/继续显示：告诉它当前是哪个方向、要做哪个动作。</summary>
        public void Show(MeterDirection direction, string actionName)
        {
            Target.SetActive(true);
            currentDirection = direction;

            if (actionText != null)
            {
                actionText.text = actionName ?? string.Empty;
            }

            for (var i = 0; i < directionArrows.Length; i++)
            {
                var arrow = directionArrows[i];
                if (arrow == null)
                {
                    continue;
                }

                var isActive = i == (int)direction - 1; // 数组顺序：上(1) 下(2) 左(3) 右(4)
                arrow.color = isActive ? arrowActiveColor : arrowIdleColor;
                arrow.rectTransform.localScale = Vector3.one;
            }
        }

        /// <summary>每帧刷新进度（0..1）。</summary>
        public void SetProgress(float progress01)
        {
            var value = Mathf.Clamp01(progress01);

            Target.SetActive(true);

            if (ringFill != null)
            {
                ringFill.fillAmount = value;
                ringFill.color = Color.Lerp(fillDimColor, fillColor, value);

                // 关键一步：让进度弧从"雷达当前扫到的位置"开始长。
                // 圆环贴图是旋转对称的，转它本身看不出来，但它的顶部跟着扇形走，
                // 于是弧线就像是被雷达"扫"出来的 —— 进度和扫描在视觉上是同一件事。
                if (radar != null)
                {
                    ringFill.rectTransform.localEulerAngles = new Vector3(0f, 0f, -radar.SweepAngle);
                }
            }

            // 当前方向的箭头随进度放大 —— 底特律那种"按住这个方向"的反馈。
            var index = (int)currentDirection - 1;
            if (index >= 0 && index < directionArrows.Length && directionArrows[index] != null)
            {
                var scale = Mathf.Lerp(1f, Mathf.Max(1f, arrowActiveScale), value);
                directionArrows[index].rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        public void Hide()
        {
            currentDirection = MeterDirection.None;

            if (ringFill != null)
            {
                ringFill.fillAmount = 0f;
                ringFill.color = fillDimColor;
                ringFill.rectTransform.localEulerAngles = Vector3.zero;
            }

            foreach (var arrow in directionArrows)
            {
                if (arrow == null)
                {
                    continue;
                }

                arrow.rectTransform.localScale = Vector3.one;
                arrow.color = arrowIdleColor;
            }

            Target.SetActive(false);
        }
    }
}
