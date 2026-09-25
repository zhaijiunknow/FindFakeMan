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
    ///  - **该方向对这个东西做不了时，完全不进进度**（进度环不会涨 ✓，箭头也是暗的 ✓）；
    ///  - 拉到位（超过 requiredDistance）后进度按 fillDuration 秒逐渐填满，**拉得越远填得越快**；
    ///  - **没填满就松手 → 进度按 releaseDecayDuration 秒缓慢回退**（不是瞬间清零），回退途中再按住同方向可以接着涨；
    ///  - 填满 → 执行该方向的 ItemAction（拾取/丢弃/装备/检视），**进度不清空** ✓：
    ///    环停在满格、动作名留着，让玩家看见"这一下成了" ✓；松手之后才按 releaseDecayDuration 缓慢回退 ✓；
    ///  - 轻点（拖拽距离没超过 dragStartThreshold）→ 只看描述，面板不关。
    ///
    /// 四个方向各绑什么动作可以在 Inspector 里改，面板上的提示文字会跟着变。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class InspectorDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
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

        /// <summary>上一次刷表盘时"目标身上那件东西"是谁 ✓（收容物解锁会换掉它 ✓，见 Update ✓）。</summary>
        private Item lastDialItem;

        /// <summary>
        /// **这次 uGUI 拖动属于"拖工具"** ✓ —— 一旦发现工具在拖就置位 ✓，直到 uGUI 自己的 `OnEndDrag` 才复位 ✓。
        ///
        /// 为什么要"锁住"整次拖动 ✗→✓：`ToolBeltInput` 在**鼠标松开**那一刻就结束了它的拖拽 ✓
        ///（`dragging = false` ✓），而 uGUI 这条要**晚一拍** ✓ ——
        /// 于是只判 `IsDragging` 的话 ✓，最后那一拍会"合法地"把家具的进度填满并执行 ✗✗
        ///（实测就是"拖工具时家具莫名其妙进了检视"✗）。
        /// </summary>
        private bool ownedByToolDrag;
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

        /// <summary>
        /// 点在**空白处** ✓（既不是家具 ✓、也不是工具槽/收容格 ✓）= **取消选中** ✓（左键右键都算 ✓）。
        ///
        /// 为什么挂在这儿 ✗→✓：`game` 板块自己吃射线 ✓（拖拽要用 ✓），而"点到空地上"才会走到这里 ✓ ——
        /// 点家具时事件被家具自己的点击处理器接走了 ✓（uGUI 找的是**最近**那个处理器 ✓），所以不会误清 ✓。
        /// 刚拖完那一下不算点击 ✓（`eventData.dragging` ✓），免得一松手就把选中清掉 ✗。
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null
                && (eventData.dragging || eventData.button == PointerEventData.InputButton.Middle))
            {
                return;
            }

            // **点在交互物上就不算"点空白"** ✓ —— 那种情况要交给交互物自己去"选中" ✓。
            // 为什么必须加这道闸 ✗→✓：事件是先冒泡到最近的点击处理器 ✓，
            // 但万一家具那层因为"alpha 命中"没吃到这一下 ✓（鼠标落在透明像素上 ✓），
            // 事件就会落到 `game` 上、也就是这里 ✓ → 若不判一下 ✓，就变成"点家具 = 取消选中"✗。
            var hit = eventData != null ? eventData.pointerCurrentRaycast.gameObject : null;
            if (hit != null && hit.GetComponentInParent<SimpleInteractable>() != null)
            {
                return;
            }

            Services.TryGet<ISceneUiView>(out var view);
            view?.ClearSelection();
        }

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
            ownedByToolDrag = false; // 这次拖动结束 ✓ → 下一次重新判是不是工具在拖 ✓
            // 没填满就不做事：进度交给 Update 缓慢回退。
        }

        // ---- 进度 ----

        private void Update()
        {
            // 选中 / 取消选中交互物时刷新表盘：箭头只在"选中了某个东西"时出现。
            var selection = hud != null ? hud.CurrentInteractable : null;

            // 除了"换了目标" ✓，**目标身上那件东西变了也要刷** ✓ ——
            // 收容物是"读过一次才解锁"的 ✓：解锁那一刻选中目标没变 ✗，
            // 不刷的话「↑拾取」箭头会一直暗着 ✓，玩家看到的就是"解锁了、却还是拿不了"✗✓。
            var dialItem = selection != null
                ? selection.AssociatedItem
                : hud != null ? hud.CurrentItem : null;

            if (!ReferenceEquals(selection, lastSelection) || !ReferenceEquals(dialItem, lastDialItem))
            {
                lastSelection = selection;
                lastDialItem = dialItem;
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

            // **道具 / 收容物也要有表盘** ✓：以前只有"选中家具"才显示 ✗ ——
            // 选中工具槽的工具、或收容格里的收容物时 ✓，玩家同样要看到
            //「←装备（放回）/ ↓丢弃 / →检视」这几个箭头 ✓。
            if (interactable == null && item == null)
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

            // 四个方向"叫什么"也一起推过去 ✓（顺序同上：上/下/左/右 ✓）——
            // 雷达扫到某个可用方向时 ✓，中心那行字就换成它的名字 ✓（拾取 / 丢弃 / 装备 / 检视 ✓）。
            progressView.SetDirectionLabels(new[]
            {
                KindName(upAction),
                KindName(downAction),
                KindName(leftAction),
                KindName(rightAction),
            });

            progressView.Show(DirectionProgressView.MeterDirection.None, string.Empty);
        }

        /// <summary>拖拽中：判断方向、累计进度。</summary>
        private void FillStep()
        {
            // **正在拖工具时不进进度** ✗→✓ ——
            // 拖工具走的是 `ToolBeltInput` 那套"拟真拖拽"（裸 `Input` 轮询 ✓），
            // 而 uGUI 会把**同一次拖动**也送进这里 ✓（`game` 实现了 `IDragHandler` ✓）→
            // 两边就一起跑 ✗：你拖工具的时候，家具的方向手势也被填满并执行 ✗✗（用户实测到了 ✓）。
            if (Services.TryGet<IToolInputService>(out var toolInput) && toolInput.IsDragging)
            {
                // 记牢"这次拖动是工具的" ✓（见 ownedByToolDrag 的注释 ✓）。
                ownedByToolDrag = true;
                return;
            }

            if (ownedByToolDrag)
            {
                // 工具那边已经松手了 ✓，但 uGUI 这次拖动还没结束 ✓ —— 一样不许填进度 ✗。
                return;
            }

            // 这一轮手势已经出过结果了 → 不再攒进度 ✗→✓。
            // 原来的毛病：Execute() 里把 progress 清零了，可玩家**还按着**（dragging 仍为 true），
            // 下一帧又从这里开始攒，于是进度环从 0 再填满一次 —— 看起来就是"体感走了两次进度" ✗，
            // 而第二次什么都不会执行（executedThisGesture 只在填满那一下检查 ✗）。
            // 顺手把环收掉：Execute 里只调了 Show（不碰 fillAmount ✓），不清会留一个满环挂在表盘上 ✗。
            if (executedThisGesture)
            {
                // 动作已经触发过：**保持进度不清空** ✓ —— 环停在满格、动作名也留着 ✓，
                // 让玩家看见"这一下成了"；松手后由 DecayStep 按 releaseDecayDuration 缓慢回退 ✓（不是啪一下没了 ✗）。
                RefreshView();
                return;
            }

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

            // 这个方向对这个东西**根本做不了** → 直接不进进度交互 ✗→✓。
            // 原来的毛病：不管能不能做都照样把进度填满，填满了走到 Execute 才发现做不了（提示「这里做不了…」✗）——
            // 玩家看到的是"进度条白走一趟"，而不是"这个方向本来就没戏" ✗。
            // 判定用的是和「箭头亮不亮」「Execute 能不能跑」完全同一套 ItemActionRunner.CanRun ✓，
            // 所以不会出现"箭头是暗的、却能拖出进度"这种自相矛盾 ✗。
            var item = hud != null ? hud.CurrentItem : null;
            var interactable = hud != null ? hud.CurrentInteractable : null;
            if (!ItemActionRunner.CanRun(kind, item, interactable))
            {
                // 顺手清掉方向记录：否则 activeKind 会一直留着，切方向时还要多走一次"换向回退" ✓（无害但没意义）。
                activeKind = ItemActionKind.None;
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
            // 双保险 ✓：万一进度在"拖工具"的过程中被填满了（例如闸门加之前就已经满 ✓），
            // 也不要在这种时刻执行家具的动作 ✗（那时玩家的意图是"把工具拖到某处"✓，不是做手势 ✓）。
            if (Services.TryGet<IToolInputService>(out var toolInput) && toolInput.IsDragging)
            {
                progress = 0f;
                RefreshView();
                return;
            }

            var item = hud != null ? hud.CurrentItem : null;
            var interactable = hud != null ? hud.CurrentInteractable : null;

            // 注意：这里**不清空** progress / activeKind ✗→✓ ——
            // 清了的话，动作刚触发环就空掉、动作名也变「无」，玩家看不到"这一下成了"✗。
            // 保持满格 + 原来的方向名 ✓，退场交给松手后的 DecayStep ✓。

            // 表盘是否收起交给"还有没有选中"决定（检视不关面板，就该继续显示）。
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
