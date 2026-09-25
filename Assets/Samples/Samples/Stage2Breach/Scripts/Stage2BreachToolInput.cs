using System.Collections.Generic;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;
using UnityEngine;

namespace Project.Samples.Stage2Breach.Scripts
{
    public sealed class Stage2BreachToolInput : MonoBehaviour, IToolInputService
    {
        [SerializeField] private ToolItem[] tools;
        [SerializeField] private int selectedSlot;

        private Camera mainCamera;
        private ToolItem draggedTool;
        private bool isDragging;

        public IReadOnlyList<ToolItem> Tools => tools;
        public int SelectedSlot => selectedSlot;
        public ToolItem CurrentTool => tools != null && selectedSlot >= 0 && selectedSlot < tools.Length ? tools[selectedSlot] : null;
        public bool IsDragging => isDragging;
        public ToolItem DraggedTool => draggedTool;

        private void Awake()
        {
            mainCamera = Camera.main;
            Services.Register<IToolInputService>(this);
        }

        private void OnDestroy()
        {
            Services.UnregisterInstance(this);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectSlot(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectSlot(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectSlot(2);

            if (!isDragging)
            {
                return;
            }

            UpdateDrag();

            if (Input.GetMouseButtonUp(0))
            {
                EndDrag();
            }
        }

        public bool TryUseOn(SimpleInteractable interactable)
        {
            if (interactable == null || !Services.TryGet<InteractionManager>(out var interactionManager))
            {
                return false;
            }

            var currentTool = CurrentTool;
            if (currentTool == null)
            {
                return false;
            }

            interactionManager.ExecuteToolInteraction(currentTool, interactable);
            return true;
        }

        public void SelectSlot(int slotIndex)
        {
            if (tools == null || slotIndex < 0 || slotIndex >= tools.Length)
            {
                return;
            }
            selectedSlot = slotIndex;

            if (Services.TryGet<UIManager>(out var uiManager))
            {
                if (tools[selectedSlot] != null)
                {
                    uiManager.ShowHint($"当前工具：{tools[selectedSlot].DisplayName}", 1.5f);
                }

                uiManager.UpdateEquipmentSlots();
            }
        }

        /// <summary>
        /// 换掉这套工具 ✓（`IToolInputService` 的成员 ✓）——
        /// 背包↔工具包换完之后 <see cref="InventoryManager"/> 会推一次 ✓；不推的话这个示例的工具条还是旧那套 ✗。
        /// </summary>
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

            // 换过之后原来选的槽位可能已经不存在了 ✓（空 = -1 ✓，不硬凑到第一格 ✗）。
            selectedSlot = tools.Length == 0 ? -1 : Mathf.Clamp(selectedSlot, 0, tools.Length - 1);

            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.UpdateEquipmentSlots();
            }
        }

        public void BeginDragSelectedTool()
        {
            BeginDrag(selectedSlot);
        }

        public void BeginDrag(int slotIndex)
        {
            SelectSlot(slotIndex);

            var tool = CurrentTool;
            if (tool == null || !Services.TryGet<InteractionManager>(out var interactionManager))
            {
                return;
            }

            draggedTool = tool;
            isDragging = true;
            interactionManager.OnToolDragStarted(tool, Input.mousePosition);
        }

        public void EndDrag()
        {
            if (!isDragging)
            {
                return;
            }

            UpdateDrag();
            TryUseDraggedTool();

            if (Services.TryGet<InteractionManager>(out var interactionManager))
            {
                interactionManager.OnToolDragEnded();
            }

            isDragging = false;
            draggedTool = null;
        }

        private void UpdateDrag()
        {
            if (Services.TryGet<InteractionManager>(out var interactionManager))
            {
                interactionManager.OnToolDragUpdated(Input.mousePosition);
            }
        }

        private void TryUseDraggedTool()
        {
            if (draggedTool == null || !Services.TryGet<InteractionManager>(out var interactionManager))
            {
                return;
            }

            EnsureCamera();
            if (mainCamera == null)
            {
                return;
            }

            var pointerPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            var hit2D = Physics2D.OverlapPoint(pointerPosition);
            var interactable = hit2D != null ? hit2D.GetComponentInParent<SimpleInteractable>() : null;
            if (interactable == null)
            {
                var ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit3D, 100f))
                {
                    interactable = hit3D.collider.GetComponentInParent<SimpleInteractable>();
                }
            }

            if (interactable == null)
            {
                if (Services.TryGet<UIManager>(out var uiManager))
                {
                    uiManager.ShowToolValidity(false);
                }
                return;
            }

            interactionManager.ExecuteToolInteraction(draggedTool, interactable);
        }

        private void EnsureCamera()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }
    }
}
