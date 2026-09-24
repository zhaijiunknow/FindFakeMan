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
    ///   2. 拖动中 → InteractionManager.OnToolDragUpdated + UIManager.UpdateToolDragIndicator（影子跟随）+ 有效性提示
    ///   3. 松手 → 射线找到指针下的交互物 → CanUseToolOn / ExecuteToolInteraction → OnToolDragEnded
    ///
    /// 输入用轮询（和 Stage2Breach 示例、VnSceneUiView 一致，项目统一走 legacy Input）。
    /// </summary>
    public sealed class ToolBeltInput : MonoBehaviour, IToolInputService
    {
        [Header("工具槽（元素对齐 BigApp 的 Items 四格）")]
        [SerializeField] private RectTransform[] slotRects = new RectTransform[0];
        [Tooltip("初始选中的槽位（一般 0 = 第一个工具）。")]
        [SerializeField] private int initialSelectedSlot;

        [Header("初始工具（也可由场景 bootstrapper 覆盖）")]
        [SerializeField] private ToolItem[] tools = new ToolItem[0];

        [Header("输入")]
        [Tooltip("鼠标移动多少像素后才算「开始拖拽」（避免点一下就被当成拖拽）。")]
        [SerializeField] private float dragStartThreshold = 6f;
        [Tooltip("是否启用数字键 1-4 快速切工具。")]
        [SerializeField] private bool enableKeyboardShortcuts = true;
        [SerializeField] private bool logInput = true;

        private int selectedSlot;
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
            selectedSlot = Mathf.Max(0, initialSelectedSlot);
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

            // 有效性提示：拖到"工具对口"的交互物上才变绿。
            // 注意不能用 InteractionManager.CanUseToolOn 判断 —— 它只判断"能不能尝试"（任何工具都返回 true），
            // 工具是否对口由交互物上的规则说了算；用错了仍然可以落下（会走失败文本 + SAN 惩罚那条路）。
            var target = FindInteractableUnderPointer(screenPosition);
            var isValid = target != null && draggingTool != null && RuleAllows(target, draggingTool);

            if (Services.TryGet<UIManager>(out var ui))
            {
                ui.ShowToolValidity(isValid);
            }
        }

        /// <summary>交互物上的规则是否接受这个工具（没有规则就当作接受）。</summary>
        private static bool RuleAllows(SimpleInteractable target, ToolItem tool)
        {
            if (target == null || tool == null)
            {
                return false;
            }

            var rule = target.GetComponent<SampleInteractableRule>();
            return rule == null || rule.CanUseTool(tool);
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
