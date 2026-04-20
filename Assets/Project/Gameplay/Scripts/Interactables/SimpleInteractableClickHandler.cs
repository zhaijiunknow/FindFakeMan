using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
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

            var handledByTool = Services.TryGet<IToolInputService>(out var toolInput) && !toolInput.IsDragging && toolInput.TryUseOn(interactable);
            if (!handledByTool && Services.TryGet<InteractionManager>(out var interactionManager))
            {
                interactionManager.OnInteractableClicked(interactable);
            }
                
        }
    }
}
