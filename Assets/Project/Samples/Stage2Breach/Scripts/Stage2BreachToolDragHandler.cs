using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.Samples.Stage2Breach.Scripts
{
    public sealed class Stage2BreachToolDragHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public int SlotIndex { get; set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            var toolInput = FindFirstObjectByType<Stage2BreachToolInput>();
            toolInput?.BeginDrag(SlotIndex);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            var toolInput = FindFirstObjectByType<Stage2BreachToolInput>();
            toolInput?.EndDrag();
        }
    }
}
