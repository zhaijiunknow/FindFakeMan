using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 精查面板的方向拖拽（设计表 §4.2「体感式交互」），带底特律式的进度可视化。
    ///
    /// 手感：
    ///  - 按住往某个方向拖 → 出现方向指示条；
    ///  - 拉到位（超过 requiredDistance）后进度按 fillDuration 秒逐渐填满，**拉得越远填得越快**；
    ///  - **没填满就松手 → 进度按 releaseDecayDuration 秒缓慢回退**（不是瞬间清零），回退途中再按住同方向可以接着涨；
    ///  - 填满 → 执行该方向的 ItemAction（拾取/丢弃/装备/检视）；
    ///  - 轻点（拖拽距离没超过 dragStartThreshold）→ 只看描述，面板不关。
    ///
    /// 四个方向各绑什么动作可以在 Inspector 里改，面板上的提示文字会跟着变。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class InspectorDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("引用")]
        [Tooltip("面板 UI：拿当前正在检视的道具/交互点，并把方向提示推过去。")]
        [SerializeField] private InvestigationHudView hud;
        [Tooltip("进度可视化（底特律式指示条）。")]
        [SerializeField] private DirectionProgressView progressView;

        [Header("手势参数")]
        [Tooltip("超过这个像素距离才算「开始拖拽」，否则当轻点（只看描述、不累计进度）。")]
        [SerializeField] private float dragStartThreshold = 24f;
        [Tooltip("方向拉到位需要的拖拽距离（像素）。不到这个距离进度几乎不涨。")]
        [SerializeField] private float requiredDistance = 90f;
        [Tooltip("拉到位时，进度从 0 填满需要几秒。")]
        [SerializeField] private float fillDuration = 0.6f;
        [Tooltip("松手（或没拉到位）时进度回退：从满格退到 0 需要几秒 —— 数值越大越慢。")]
        [SerializeField] private float releaseDecayDuration = 1.4f;

        [Header("四个方向各绑什么动作")]
        [SerializeField] private ItemActionKind upAction = ItemActionKind.Pickup;
        [SerializeField] private ItemActionKind downAction = ItemActionKind.Discard;
        [SerializeField] private ItemActionKind leftAction = ItemActionKind.Equip;
        [SerializeField] private ItemActionKind rightAction = ItemActionKind.Inspect;

        [Header("调试")]
        [SerializeField] private bool logActions = true;

        private Vector2 dragStart;
        private Vector2 currentDelta;
        private bool dragging;
        private bool executedThisGesture;
        private SimpleInteractable lastSelection;
        private float progress;
        private ItemActionKind activeKind = ItemActionKind.None;
        private Vector2 activeDirection;

        private void Start()
        {
            // Start 晚于 Awake，这时 HUD 一定已经就绪。
            PushDirectionHints();
            progressView?.Hide();
        }

        // ---- 手指/鼠标 ----

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = true;
            executedThisGesture = false;
            dragStart = eventData.position;
            currentDelta = Vector2.zero;

            // 注意：不清零进度 —— 上一次松手后回退到一半时再抓住，可以接着涨。
        }

        public void OnDrag(PointerEventData eventData)
        {
            currentDelta = eventData.position - dragStart;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            dragging = false;
            currentDelta = Vector2.zero;
            // 没填满就不做事：进度交给 Update 缓慢回退。
        }

        // ---- 进度 ----

        private void Update()
        {
            // 选中 / 取消选中交互物时刷新表盘：箭头只在"选中了某个东西"时出现。
            var selection = hud != null ? hud.CurrentInteractable : null;
            if (!ReferenceEquals(selection, lastSelection))
            {
                lastSelection = selection;
                RefreshSelectionVisuals();
            }

            if (dragging)
            {
                FillStep();
            }
            else
            {
                DecayStep();
            }
        }

        /// <summary>
        /// 选中了交互物 → 显示表盘（四个箭头 + 进度环；底环和扫描扇形是常驻的，不归这里开关），
        /// 并把四个方向"现在能不能做"推给视图。
        /// 判定用的是和拖拽执行同一套 <see cref="ItemActionRunner.CanRun"/>，
        /// 所以不会出现"箭头显示能用、拖了却没反应"。
        /// 没选中 → 收起箭头与进度环。
        /// </summary>
        private void RefreshSelectionVisuals()
        {
            if (progressView == null)
            {
                return;
            }

            var item = hud != null ? hud.CurrentItem : null;
            var interactable = hud != null ? hud.CurrentInteractable : null;

            if (interactable == null)
            {
                if (!dragging && progress <= 0f)
                {
                    progressView.Hide();
                }

                return;
            }

            progressView.SetAvailableDirections(new[]
            {
                ItemActionRunner.CanRun(upAction, item, interactable),
                ItemActionRunner.CanRun(downAction, item, interactable),
                ItemActionRunner.CanRun(leftAction, item, interactable),
                ItemActionRunner.CanRun(rightAction, item, interactable),
            });

            progressView.Show(DirectionProgressView.MeterDirection.None, string.Empty);
        }

        /// <summary>拖拽中：判断方向、累计进度。</summary>
        private void FillStep()
        {
            var distance = currentDelta.magnitude;
            if (distance < dragStartThreshold)
            {
                DecayStep();
                return;
            }

            var kind = ResolveDirection(currentDelta);
            if (kind == ItemActionKind.None)
            {
                DecayStep();
                return;
            }

            // 中途换方向：这一份进度按回退处理，不直接清零，也不叠加到新方向上。
            if (activeKind != ItemActionKind.None && activeKind != kind)
            {
                DecayStep();
                return;
            }

            activeKind = kind;
            activeDirection = currentDelta;

            // 拉得越远填得越快（到 requiredDistance 时是满速）。
            var pull = Mathf.Clamp01(distance / Mathf.Max(1f, requiredDistance));
            progress = Mathf.Clamp01(progress + Time.unscaledDeltaTime * pull / Mathf.Max(0.05f, fillDuration));

            RefreshView();

            if (progress >= 1f && !executedThisGesture)
            {
                executedThisGesture = true;
                Execute(kind);
            }
        }

        /// <summary>松手或方向不对：进度缓慢回退（这就是"没完成就松手"的表现）。</summary>
        private void DecayStep()
        {
            if (progress <= 0f)
            {
                progress = 0f;
                activeKind = ItemActionKind.None;

                // 退到 0 不等于"收起表盘"：还选中着东西就继续显示（只是进度环空了）。
                RefreshSelectionVisuals();
                return;
            }

            progress = Mathf.Max(0f, progress - Time.unscaledDeltaTime / Mathf.Max(0.05f, releaseDecayDuration));
            RefreshView();
        }

        private void RefreshView()
        {
            if (progressView == null)
            {
                return;
            }

            if (progress <= 0f)
            {
                progressView.Hide();
                return;
            }

            progressView.Show(ToMeterDirection(activeDirection), KindName(activeKind));
            progressView.SetProgress(progress);
        }

        /// <summary>把拖拽向量映射成表盘方向（同一套主方向判定）。</summary>
        private static DirectionProgressView.MeterDirection ToMeterDirection(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return delta.x >= 0f ? DirectionProgressView.MeterDirection.Right : DirectionProgressView.MeterDirection.Left;
            }

            return delta.y >= 0f ? DirectionProgressView.MeterDirection.Up : DirectionProgressView.MeterDirection.Down;
        }

        // ---- 执行 ----

        private void Execute(ItemActionKind kind)
        {
            var item = hud != null ? hud.CurrentItem : null;
            var interactable = hud != null ? hud.CurrentInteractable : null;

            progress = 0f;
            activeKind = ItemActionKind.None;

            // 动作做完：进度清零。表盘是否收起交给"还有没有选中"决定（检视不关面板，就该继续显示）。
            RefreshSelectionVisuals();

            if (!ItemActionRunner.CanRun(kind, item, interactable))
            {
                if (Services.TryGet<UIManager>(out var uiManager))
                {
                    uiManager.ShowHint($"这里做不了「{KindName(kind)}」。", 2f);
                }

                return;
            }

            var ok = ItemActionRunner.Run(kind, item, interactable);
            if (logActions)
            {
                Debug.Log($"[Inspector] {DirectionName(activeDirection)}方向「{KindName(kind)}」完成，" +
                          $"item={(item != null ? item.DisplayName : "null")}，" +
                          $"interactable={(interactable != null ? interactable.InteractableId : "null")}，ok={ok}");
            }

            if (!ok)
            {
                return;
            }

            if (Services.TryGet<UIManager>(out var ui))
            {
                // 装备可能变了（工具槽/耐久），刷新一下。
                ui.UpdateEquipmentSlots();

                // 除了「检视」，动作执行完就收起面板（设计表 §4.2：动作执行 → 关闭面板）。
                // 右拖=检视：这才是"放大看清"的动作（点一下只是选中 + 开详情区，不该直接放大）。
                if (kind == ItemActionKind.Inspect)
                {
                    if (interactable != null)
                    {
                        interactable.SetZoomed(true);
                    }
                }
                else
                {
                    ui.HideInspector();
                }
            }
        }

        // ---- 方向与文案 ----

        /// <summary>把当前的方向绑定推给 HUD（显示在名称那一行）。</summary>
        public void PushDirectionHints()
        {
            if (hud == null)
            {
                hud = GetComponentInParent<InvestigationHudView>();
            }

            hud?.SetDirectionHints(BuildDirectionHintText());
        }

        private string BuildDirectionHintText()
        {
            var text = string.Empty;
            AppendHint(ref text, "上", upAction);
            AppendHint(ref text, "下", downAction);
            AppendHint(ref text, "左", leftAction);
            AppendHint(ref text, "右", rightAction);
            return text;
        }

        private static void AppendHint(ref string text, string direction, ItemActionKind kind)
        {
            if (kind == ItemActionKind.None)
            {
                return;
            }

            if (text.Length > 0)
            {
                text += "  ";
            }

            text += $"{direction}{KindName(kind)}";
        }

        private ItemActionKind ResolveDirection(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return delta.x >= 0f ? rightAction : leftAction;
            }

            return delta.y >= 0f ? upAction : downAction;
        }

        private static string DirectionName(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return delta.x >= 0f ? "右" : "左";
            }

            return delta.y >= 0f ? "上" : "下";
        }

        private static string KindName(ItemActionKind kind)
        {
            switch (kind)
            {
                case ItemActionKind.Pickup: return "拾取";
                case ItemActionKind.Discard: return "丢弃";
                case ItemActionKind.Inspect: return "检视";
                case ItemActionKind.Equip: return "装备";
                default: return "无";
            }
        }
    }
}
