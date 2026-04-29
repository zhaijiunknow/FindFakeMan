using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.Samples.Stage2Breach.Scripts
{
    public sealed class Stage2BreachToolDragHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private int slotIndex;

        public int SlotIndex
        {
            get => slotIndex;
            set => slotIndex = value;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            var toolInput = FindFirstObjectByType<Stage2BreachToolInput>();
            toolInput?.BeginDrag(slotIndex);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            var toolInput = FindFirstObjectByType<Stage2BreachToolInput>();
            toolInput?.EndDrag();
        }
    }
}
