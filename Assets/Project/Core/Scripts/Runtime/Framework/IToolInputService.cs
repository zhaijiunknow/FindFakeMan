using System.Collections.Generic;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;

namespace Project.Core.Runtime.Framework
{
    public interface IToolInputService
    {
        IReadOnlyList<ToolItem> Tools { get; }
        int SelectedSlot { get; }
        bool IsDragging { get; }
        bool TryUseOn(SimpleInteractable interactable);
        void SelectSlot(int slotIndex);
        void BeginDragSelectedTool();
        void BeginDrag(int slotIndex);
        void EndDrag();

        /// <summary>
        /// 换掉工具条上这套工具 ✓（背包↔工具包配置完必须推一次 ✗ ——
        /// 工具条是"已装备工具"的**视图** ✓，不推的话换了装备它还显示/拖出旧那套 ✗）。
        /// </summary>
        void SetTools(IReadOnlyList<ToolItem> tools);
    }
}
