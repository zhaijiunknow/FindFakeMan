using UnityEngine;
using Project.Core.Runtime.Framework;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;

namespace Project.Core.Runtime.Managers
{
    public sealed class InteractionManager : ManagerBehaviour
    {
        private readonly System.Collections.Generic.List<SimpleInteractable> interactables = new();
        private bool isInteractionPaused;

        public void Register(SimpleInteractable interactable)
        {
            if (interactable != null && !interactables.Contains(interactable))
            {
                interactables.Add(interactable);
            }
        }

        public void Unregister(SimpleInteractable interactable)
        {
            if (interactable != null)
            {
                interactables.Remove(interactable);
            }
        }

        public void PauseInteractions() => isInteractionPaused = true;
        public void ResumeInteractions() => isInteractionPaused = false;

        public void OnInteractableClicked(SimpleInteractable interactable)
        {
            if (isInteractionPaused || interactable == null || !interactable.IsActive)
            {
                return;
            }

            if (Services.TryGet<GameManager>(out var gameManager))
            {
                gameManager.SwitchState(GameState.Inspection);
            }

            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.ShowInspector(interactable.AssociatedItem, interactable);
            }

            var sampleRule = interactable.GetComponent<SampleInteractableRule>();
            if (sampleRule != null && sampleRule.ResolveOnClick)
            {
                if (sampleRule.TryResolveFromClick())
                {
                    OnActionExecuted(ActionResult.Success);
                }
                else
                {
                    OnActionExecuted(ActionResult.Invalid);
                }
            }
        }

        public void OnToolDragStarted(ToolItem toolItem, Vector2 position)
        {
            Services.TryGet<UIManager>(out var uiManager);
            uiManager?.ShowToolDragIndicator(toolItem?.Icon, position);
        }

        public void OnToolDragUpdated(Vector2 position)
        {
            Services.TryGet<UIManager>(out var uiManager);
            uiManager?.UpdateToolDragIndicator(position);
        }

        public void OnToolDragEnded()
        {
            Services.TryGet<UIManager>(out var uiManager);
            uiManager?.HideToolDragIndicator();
        }

        public bool CanUseToolOn(SimpleInteractable interactable, ToolItem toolItem)
        {
            return !isInteractionPaused && interactable != null && toolItem != null && interactable.IsActive;
        }

        public void ExecuteToolInteraction(ToolItem toolItem, SimpleInteractable interactable)
        {
            if (!CanUseToolOn(interactable, toolItem))
            {
                OnActionExecuted(ActionResult.Invalid);
                return;
            }

            var sampleRule = interactable.GetComponent<SampleInteractableRule>();
            if (sampleRule != null)
            {
                OnActionExecuted(sampleRule.TryResolveWithTool(toolItem) ? ActionResult.Success : ActionResult.Invalid);
                return;
            }

            OnActionExecuted(ActionResult.Success);
        }

        public void OnCollected(SimpleInteractable interactable)
        {
            interactable?.SetCollected();
        }

        public void OnActionExecuted(ActionResult result)
        {
            Debug.Log($"Interaction action executed: {result}");
        }
    }
}
