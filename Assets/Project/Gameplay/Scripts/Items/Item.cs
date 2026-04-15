using UnityEngine;

namespace Project.Gameplay.Scripts.Items
{
    public class Item : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] [TextArea] private string description;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public string Description => description;
    }
}
