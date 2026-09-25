using System.Collections.Generic;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Gameplay.Scripts.Tools
{
    /// <summary>
    /// Project 侧的 <see cref="IToolInputService"/>：工具栏选择 + **拟真拖拽**（按住工具槽拖到房间里松手）。
    ///
    /// 拖拽全流程（对应设计规范「工具拖拽流程」）：
    ///   1. 在工具槽上按下 → <see cref="SelectSlot"/>，鼠标移动超过阈值 → <see cref="BeginDrag"/>（广播 OnToolDragStarted + 出现拖拽影子）
    ///   2. 拖动中 → InteractionManager.OnToolDragUpdated + UIManager.UpdateToolDragIndicator（影子跟随，**不提示对不对** ✓）
    ///   3. 松手 → 射线找到指针下的交互物 → CanUseToolOn / ExecuteToolInteraction → OnToolDragEnded
    ///
    /// 输入用轮询（和 Stage2Breach 示例、VnSceneUiView 一致，项目统一走 legacy Input）。
    /// </summary>
    public sealed class ToolBeltInput : MonoBehaviour, IToolInputService
    {
        [Header("工具槽（元素对齐 BigApp 的 Items 四格）")]
        [SerializeField] private RectTransform[] slotRects = new RectTransform[0];
        [Tooltip("初始选中的槽位。-1 = 进关卡时什么都不选（默认 ✓）；0..N-1 = 预选某一格。")]
        [SerializeField] private int initialSelectedSlot = -1;

        [Header("初始工具（也可由场景 bootstrapper 覆盖）")]
        [SerializeField] private ToolItem[] tools = new ToolItem[0];

        [Header("输入")]
        [Tooltip("鼠标移动多少像素后才算「开始拖拽」（避免点一下就被当成拖拽）。")]
        [SerializeField] private float dragStartThreshold = 6f;
        [Tooltip("是否启用数字键 1-4 快速切工具。")]
        [SerializeField] private bool enableKeyboardShortcuts = true;
        [SerializeField] private bool logInput = true;

        // -1 = 还没选任何工具（进关卡时的初始状态 ✓）；只有点了槽位才会变成 0..N-1 ✓
        private int selectedSlot = -1;
        private bool armed;               // 在槽位上按下了，但还没算拖拽
        private Vector2 armPosition;
        private bool dragging;
        private ToolItem draggingTool;

        public IReadOnlyList<ToolItem> Tools => tools;
        public int SelectedSlot => selectedSlot;
        public bool IsDragging => dragging;

        private void Awake()
        {
            Services.Register<IToolInputService>(this);
            // 允许 -1（= 没选中）✓ —— 原来用 Max(0,…) 会把 -1 夹成 0 ✗，
            // 结果一进关卡就等于"选中了第一格"，底部工具槽第 1 格一直高亮 ✗。
            selectedSlot = initialSelectedSlot < 0
                ? -1
                : Mathf.Clamp(initialSelectedSlot, 0, Mathf.Max(0, slotRects.Length - 1));
        }

        private void OnDestroy()
        {
            Services.UnregisterInstance(this);
        }

        private void Update()
        {
            if (enableKeyboardShortcuts)
            {
                PollKeyboard();
            }

            // **右键 = 取消这次拖拽** ✓（工具不落地、不消耗耐久 ✓）：
            // `EndDrag()` 只收影子 + 通知 ✓，不执行任何交互 ✓ —— 正是"取消"该有的行为 ✓。
            if (dragging && Input.GetMouseButtonDown(1))
            {
                armed = false;
                EndDrag();

                if (logInput)
                {
                    Debug.Log("[ToolBelt] 右键取消拖拽 ✓");
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                BeginPress(Input.mousePosition);
            }

            if (armed && !dragging && ((Vector2)Input.mousePosition - armPosition).sqrMagnitude >= dragStartThreshold * dragStartThreshold)
            {
                BeginDrag(selectedSlot);
            }

            if (dragging)
            {
                if (Input.GetMouseButton(0))
                {
                    UpdateDrag(Input.mousePosition);
                }

                if (Input.GetMouseButtonUp(0))
                {
                    ReleaseDrag(Input.mousePosition);
                }
            }
            else if (armed && Input.GetMouseButtonUp(0))
            {
                armed = false; // 只是点了一下槽位，等于选中
            }
        }

        // ---- IToolInputService ----

        public void SelectSlot(int slotIndex)
        {
            if (slotRects.Length == 0)
            {
                return;
            }

            selectedSlot = Mathf.Clamp(slotIndex, 0, slotRects.Length - 1);
            RefreshSlotUi();
        }

        /// <summary>
        /// 清空选择：回到「手上什么都没拿」的状态（底部工具槽全部不亮 ✓）。
        ///
        /// 为什么要有这个方法，而不是靠 <see cref="initialSelectedSlot"/> 的默认值：
        /// 那个字段**是序列化的**，场景里存着 `initialSelectedSlot: 0`（= 第一格）——
        /// 改代码里的默认值对已经存过的场景一点用都没有 ✗（序列化值优先 ✓），
        /// 所以"进关卡不预选"这件事必须在**运行时**做一次，才不用重建场景 ✓。
        /// </summary>
        public void ClearSelection()
        {
            selectedSlot = -1;
            RefreshSlotUi();

            if (logInput)
            {
                Debug.Log("[ToolBelt] 清空工具选择：进关卡默认不预选任何工具 ✓");
            }
        }

        public void BeginDragSelectedTool()
        {
            BeginDrag(selectedSlot);
        }

        public void BeginDrag(int slotIndex)
        {
            SelectSlot(slotIndex);
            var tool = GetTool(selectedSlot);
            if (tool == null)
            {
                ShowResult("这个槽位没有工具。", false);
                armed = false;
                return;
            }

            if (tool.Durability <= 0)
            {
                ShowResult($"{tool.DisplayName} 已经没电/用完了。", true);
                armed = false;
                return;
            }

            dragging = true;
            draggingTool = tool;
            armed = false;

            if (Services.TryGet<InteractionManager>(out var interactionManager))
            {
                // 拖拽影子由 OnToolDragStarted → UIManager.ShowToolDragIndicator → 场景 UI 显示，这里不要重复调一次。
                interactionManager.OnToolDragStarted(tool, Input.mousePosition);
            }

            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.ShowHint($"把 {tool.DisplayName} 拖到可疑的地方。", 1.4f);
            }

            if (logInput)
            {
                Debug.Log($"[ToolBelt] 开始拖拽：{tool.DisplayName}（{tool.ToolType}，耐久 {tool.Durability}/{tool.MaxDurability}）");
            }
        }

        public void EndDrag()
        {
            if (!dragging)
            {
                armed = false;
                return;
            }

            dragging = false;
            draggingTool = null;

            if (Services.TryGet<InteractionManager>(out var interactionManager))
            {
                // 收影子也走 OnToolDragEnded → UIManager.HideToolDragIndicator，这里不要重复调一次。
                interactionManager.OnToolDragEnded();
            }
        }

        /// <summary>用当前选中的工具作用到某个交互物上（点选路径也走这里）。</summary>
        public bool TryUseOn(SimpleInteractable interactable)
        {
            if (interactable == null)
            {
                return false;
            }

            var tool = GetTool(selectedSlot);
            if (tool == null)
            {
                ShowResult("没有选中工具。", true);
                return false;
            }

            if (!Services.TryGet<InteractionManager>(out var interactionManager))
            {
                Debug.LogWarning("[ToolBelt] 场景里没有 InteractionManager，无法执行工具交互。");
                return false;
            }

            if (!interactionManager.CanUseToolOn(interactable, tool))
            {
                ShowResult($"{tool.DisplayName} 在这里用不上。", true);
                return false;
            }

            interactionManager.ExecuteToolInteraction(tool, interactable);
            RefreshSlotUi();
            return true;
        }

        /// <summary>由场景 bootstrapper 在初始化时灌入装备的工具。</summary>
        public void SetTools(IReadOnlyList<ToolItem> source)
        {
            if (source == null)
            {
                tools = new ToolItem[0];
            }
            else
            {
                tools = new ToolItem[source.Count];
                for (var i = 0; i < source.Count; i++)
                {
                    tools[i] = source[i];
                }
            }

            RefreshSlotUi();
        }

        // ---- 内部 ----

        private void PollKeyboard()
        {
            for (var i = 0; i < slotRects.Length && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SelectSlot(i);
                    return;
                }
            }
        }

        private void BeginPress(Vector2 screenPosition)
        {
            var slot = SlotIndexAt(screenPosition);
            if (slot < 0)
            {
                return;
            }

            SelectSlot(slot);
            armed = true;
            armPosition = screenPosition;
        }

        private void UpdateDrag(Vector2 screenPosition)
        {
            if (Services.TryGet<InteractionManager>(out var interactionManager))
            {
                interactionManager.OnToolDragUpdated(screenPosition);
            }

            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.UpdateToolDragIndicator(screenPosition);
            }

            // **不再报"有效/无效"** ✗→✓ —— 原来拖到"工具对口"的目标上影子会变绿 ✗，
            // 那等于提前把答案递给玩家 ✓，把"自己试、自己判断"的难度整个抹掉了 ✗。
            // 对标恐鬼症：随便拖、随便用 ✓，读不读得出东西是玩家自己的事 ✓。
            // 所以这里只让影子跟随 ✓；颜色在 InvestigationHudView 那边恒为中性 ✓。
        }

        private void ReleaseDrag(Vector2 screenPosition)
        {
            var target = FindInteractableUnderPointer(screenPosition);
            if (target == null)
            {
                ShowResult("松手的地方没有可以调查的东西。", false);
                EndDrag();
                return;
            }

            var tool = draggingTool;
            EndDrag();
            if (tool == null)
            {
                return;
            }

            // 松手的落点用"当时拖着的工具"，不是当前选中的槽位。
            var slot = IndexOf(tool);
            if (slot >= 0)
            {
                selectedSlot = slot;
            }

            TryUseOn(target);
        }

        private int SlotIndexAt(Vector2 screenPosition)
        {
            for (var i = 0; i < slotRects.Length; i++)
            {
                var rect = slotRects[i];
                if (rect == null || !rect.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var camera = CanvasCamera(rect);
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, camera))
                {
                    return i;
                }
            }

            return -1;
        }

        private static Camera CanvasCamera(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera;
        }

        private static SimpleInteractable FindInteractableUnderPointer(Vector2 screenPosition)
        {
            if (EventSystem.current == null)
            {
                return null;
            }

            var data = new PointerEventData(EventSystem.current) { position = screenPosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);
            foreach (var result in results)
            {
                if (result.gameObject == null)
                {
                    continue;
                }

                var found = result.gameObject.GetComponentInParent<SimpleInteractable>();
                if (found == null)
                {
                    // 特写这类"盖住房间的整帧图"上挂的是落点代理：它代表底下那件家具。
                    // 不认代理的话，玩家在特写正中松手会落在底下那张图的透明处 → "松手的地方没有可以调查的东西"。
                    var proxy = result.gameObject.GetComponentInParent<IInteractableHitProxy>();
                    if (proxy != null)
                    {
                        found = proxy.HitTarget;
                    }
                }

                if (found != null && found.IsActive)
                {
                    return found;
                }
            }

            return null;
        }

        private ToolItem GetTool(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= tools.Length)
            {
                return null;
            }

            return tools[slotIndex];
        }

        private int IndexOf(ToolItem tool)
        {
            for (var i = 0; i < tools.Length; i++)
            {
                if (ReferenceEquals(tools[i], tool))
                {
                    return i;
                }
            }

            return -1;
        }

        private void RefreshSlotUi()
        {
            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.UpdateEquipmentSlots();
            }
        }

        private static void ShowResult(string content, bool highlight)
        {
            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.ShowToolResult(content, highlight);
            }
        }
    }
}
