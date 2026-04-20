using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using UnityEngine;

namespace Project.Gameplay.Scripts.Interactables
{
    [RequireComponent(typeof(SimpleInteractable))]
    public sealed class SimpleInteractableAutoRegister : MonoBehaviour
    {
        private SimpleInteractable interactable;
        private bool registered;

        private void Awake()
        {
            interactable = GetComponent<SimpleInteractable>();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            if (!registered)
            {
                return;
            }

            if (Services.TryGet<InteractionManager>(out var interactionManager))
            {
                interactionManager.Unregister(interactable);
            }

            registered = false;
        }

        private void TryRegister()
        {
            if (registered || interactable == null)
            {
                return;
            }

            if (Services.TryGet<InteractionManager>(out var interactionManager))
            {
                interactionManager.Register(interactable);
                registered = true;
            }
        }
    }
}
