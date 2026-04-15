using UnityEngine;
using Project.Gameplay.Scripts.Items;

namespace Project.Gameplay.Scripts.Interactables
{
    public class SimpleInteractable : MonoBehaviour
    {
        [SerializeField] private string interactableId;
        [SerializeField] private Item associatedItem;
        [SerializeField] private bool isActive = true;
        [SerializeField] private bool isCollected;
        [SerializeField] private string interactionState = "default";
        [SerializeField] [TextArea] private string description;
        [SerializeField] [TextArea] private string anomalyDescription;

        public string InteractableId => interactableId;
        public Item AssociatedItem => associatedItem;
        public bool IsActive => isActive;
        public bool IsCollected => isCollected;
        public string InteractionState => interactionState;
        public string Description => description;
        public string AnomalyDescription => anomalyDescription;

        public void SetActive(bool value)
        {
            isActive = value;
        }

        public void SetCollected()
        {
            isCollected = true;
            isActive = false;
        }

        public void SetInteractionState(string state)
        {
            interactionState = state;
        }
    }
}
