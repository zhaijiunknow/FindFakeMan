using UnityEngine;
using Project.Core.Runtime.Framework;

namespace Project.Gameplay.Scripts.Items
{
    [CreateAssetMenu(menuName = "Project/Items/Tool Item")]
    public class ToolItem : Item
    {
        [SerializeField] private int maxDurability = 5;
        [SerializeField] private int durability = 5;
        [SerializeField] private ToolType toolType;

        public int MaxDurability => maxDurability;
        public int Durability => durability;
        public ToolType ToolType => toolType;

        public bool Use()
        {
            if (durability <= 0)
            {
                return false;
            }

            durability--;
            return true;
        }

        public void RestoreDurability()
        {
            durability = maxDurability;
        }
    }
}
