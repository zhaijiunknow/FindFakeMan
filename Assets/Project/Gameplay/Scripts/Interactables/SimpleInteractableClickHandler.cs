using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Samples.Stage2Breach.Scripts;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.Gameplay.Scripts.Interactables
{
    [RequireComponent(typeof(SimpleInteractable))]
    [RequireComponent(typeof(Collider))]
    public sealed class SimpleInteractableClickHandler : MonoBehaviour
    {
        private SimpleInteractable interactable;

        private void Awake()
        {
            interactable = GetComponent<SimpleInteractable>();
        }

        private void OnMouseDown()
        {
            if (interactable == null)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (Services.TryGet<InteractionManager>(out var interactionManager))
            {
                interactionManager.OnInteractableClicked(interactable);
            }

            var toolInput = FindFirstObjectByType<Stage2BreachToolInput>();
            if (toolInput != null && !toolInput.IsDragging)
            {
                toolInput.TryUseOn(interactable);
            }
        }
    }
}
