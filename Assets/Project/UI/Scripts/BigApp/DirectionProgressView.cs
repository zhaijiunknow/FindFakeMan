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

        /// <summary>
        /// 四个方向"那件事叫什么" ✓（拾取 / 丢弃 / 装备 / 检视 ✓），由 <see cref="InspectorDragHandler"/> 推过来 ✓。
        /// 雷达扫到某个可用方向时 ✓，中心那行字就用它 ✓ —— 箭头只是亮一下 ✓，文字才说得清这是什么 ✓。
        /// </summary>
        private readonly string[] directionLabels = new string[4];

        /// <summary>拖拽本身那条动作名 ✓（雷达没扫到任何箭头时，中心显示回它 ✓）。</summary>
        private string baseActionText = string.Empty;

        /// <summary>
        /// 被雷达扫到的那条名字的**余晖** ✓（1 = 刚被扫到 ✓ → 慢慢减淡到 0 ✓）。
        ///
        /// 为什么做成减淡 ✗→✓：雷达前缘那颗红点是"亮着来、拖着尾巴淡出去"✓ ——
        /// 中心那行字要是"扫过去就啪地消失"✗，就和雷达不是一个节奏了 ✓。
        /// </summary>
        private float sweptLabelAlpha;

        /// <summary>有人请求过"收起"✓，但当时还有余晖没散尽 ✓ → 先记下 ✓，等 Update 里散尽再真正关 ✓。</summary>
        private bool hideRequested;

        /// <summary>
        /// **拖拽那条动作名**（`baseActionText` ✓，比如「丢弃」✓）的亮度 ✓（1 = 正常显示 ✓）。
        /// 和"雷达余晖"（`sweptLabelAlpha` ✓）是两个来源 ✓，取较亮的那个来画 ✓ ——
        /// 收起时两个一起淡 ✓，所以"拖动结束"和"雷达扫过"都会慢慢消失 ✓，而不是各消失各的 ✗。
        /// </summary>
        private float labelAlpha = 1f;

        [Tooltip("余晖散尽要几秒 ✓（越小消失越快 ✓）。")]
        [SerializeField] private float sweptLabelDecay = 1.6f;

        /// <summary>四个方向现在能不能做（上/下/左/右），由 InspectorDragHandler 在选中物品时算好推过来。</summary>
        public void SetAvailableDirections(bool[] available)
        {
            for (var i = 0; i < directionAvailable.Length; i++)
            {
                directionAvailable[i] = available != null && i < available.Length && available[i];
            }
        }

        /// <summary>四个方向的名字 ✓（和 <see cref="SetAvailableDirections"/> 同顺序 ✓：上/下/左/右 ✓）。</summary>
        public void SetDirectionLabels(string[] labels)
        {
            for (var i = 0; i < directionLabels.Length; i++)
            {
                directionLabels[i] = labels != null && i < labels.Length ? labels[i] : string.Empty;
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
                var swept = available && Mathf.Abs(Mathf.DeltaAngle(-radar.SweepAngle, DirectionAngle(i))) <= sweepHitTolerance;
                arrow.color = !available ? arrowIdleColor : swept ? arrowSweptColor : arrowAvailableColor;
            }

            // **雷达扫到哪个可用方向，就在中心把那件事的名字说出来** ✓ ——
            // 箭头变色只持续一瞬 ✓，玩家未必看得清"这个方向是干什么的"✗；
            // 中心那行字（`actionText` ✓）正好是空的 ✓，用它最合适 ✓。
            if (actionText != null)
            {
                var sweptIndex = FindSweptDirection();
                var sweptLabel = sweptIndex >= 0 ? directionLabels[sweptIndex] : null;

                if (!string.IsNullOrEmpty(sweptLabel))
                {
                    // 刚被扫到：换成它的名字 ✓ 并点亮 ✓（接下来交给下面的余晖接管 ✓）。
                    actionText.text = sweptLabel;
                    sweptLabelAlpha = 1f;
                }
                else if (sweptLabelAlpha > 0f)
                {
                    // 扫过去了：**留着那行字慢慢减淡** ✓（和雷达前缘红点那条尾巴同一个节奏 ✓）。
                    sweptLabelAlpha = Mathf.MoveTowards(sweptLabelAlpha, 0f,
                        Time.unscaledDeltaTime / Mathf.Max(0.05f, sweptLabelDecay));

                    if (sweptLabelAlpha <= 0f)
                    {
                        actionText.text = baseActionText; // 余晖散尽 → 回到拖拽那条动作名 ✓
                    }
                }
                else
                {
                    actionText.text = baseActionText;
                }

                // **收到收起请求 → 两个来源一起淡** ✓（雷达余晖 ✓ + 拖拽动作名 ✓），
                // 淡到 0 才真的清字 + 收起 ✓（收起的延后见 Hide() ✓）。
                if (hideRequested)
                {
                    labelAlpha = Mathf.MoveTowards(labelAlpha, 0f,
                        Time.unscaledDeltaTime / Mathf.Max(0.05f, sweptLabelDecay));

                    if (labelAlpha <= 0f && sweptLabelAlpha <= 0f)
                    {
                        actionText.text = string.Empty;
                        hideRequested = false;
                        Hide(); // 此时两个 alpha 都是 0 ✓ → Hide 会真的关掉 ✓
                        return;
                    }
                }

                // 两条淡出各自独立 ✓，谁更暗听谁的 ✓（取较小值 ✓）：
                //  ・雷达余晖（`sweptLabelAlpha` ✓）：扫过去之后自己往回走 ✓（不管有没有收起请求 ✓）；
                //  ・收起请求（`labelAlpha` ✓）：只剩拖拽那条动作名时由它负责淡 ✓。
                var alpha = Mathf.Min(
                    sweptLabelAlpha > 0f ? sweptLabelAlpha : 1f,
                    hideRequested ? labelAlpha : 1f);

                var color = actionText.color;
                color.a = alpha > 0f ? alpha : 1f;
                actionText.color = color;
            }
        }

        /// <summary>雷达此刻正扫在哪个**可用**方向上 ✓（没有就是 -1 ✓）。</summary>
        private int FindSweptDirection()
        {
            for (var i = 0; i < directionArrows.Length; i++)
            {
                if (!directionAvailable[i])
                {
                    continue;
                }

                if (Mathf.Abs(Mathf.DeltaAngle(-radar.SweepAngle, DirectionAngle(i))) <= sweepHitTolerance)
                {
                    return i;
                }
            }

            return -1;
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
                baseActionText = actionName ?? string.Empty;

                // **空名字时不许直接清字** ✗→✓：空闲状态（没拖拽）下 `Show(None, "")` 会被反复调用 ✓，
                // 直接写就会把中心那行字**当场抹掉** ✗ —— 余晖根本没机会淡 ✓（实测就是"正常还是突然消失"✗）。
                // 只有"确实有一条动作名"时才立刻写 ✓；空的时候交给 `Update` 的减淡去收尾 ✓。
                if (!string.IsNullOrEmpty(baseActionText) || sweptLabelAlpha <= 0f)
                {
                    actionText.text = baseActionText;

                    if (!string.IsNullOrEmpty(baseActionText))
                    {
                        labelAlpha = 1f;      // 新动作名进来了 → 重新点亮 ✓
                        hideRequested = false; // 顺手撤掉还没做完的收起请求 ✓，否则新字一出现就顺着往下淡 ✗
                    }
                }
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
                    ringFill.rectTransform.localEulerAngles = new Vector3(0f, 0f, radar.SweepAngle);
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

            // **还有字没淡完就先别关** ✗→✓：中心那行字（`ActionText` ✓）和箭头 / 进度环**同住在 `Target` 里** ✓ ——
            // 一关，`Update` 立刻不再执行 ✓（它开头就判 `!Target.activeSelf` ✓）→ 淡出永远跑不完 ✓ →
            // 表现就是"啪地整块消失"✗（实测两种来源都撞过 ✓：雷达余晖 ✓、拖拽那条动作名 ✓）。
            // 所以两个来源都算 ✓（取较亮的那个 ✓），只**记下请求** ✓，淡尽之后由 `Update` 真正关掉 ✓。
            // 注意 `actionText` 没接上时**不许推迟** ✗：那时 `Update` 里整块淡出不会执行 ✓ →
            // 亮度永远是 1 ✗ → 会永远收不回去（雷达环挂死在屏幕上 ✗）。
            if (actionText != null && Mathf.Max(labelAlpha, sweptLabelAlpha) > 0.001f)
            {
                hideRequested = true;
                return;
            }

            Target.SetActive(false);
        }
    }
}
