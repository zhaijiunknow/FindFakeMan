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
    }
}
