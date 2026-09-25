using UnityEngine;
using Project.Core.Runtime.Framework;

namespace Project.Gameplay.Scripts.Items
{
    [CreateAssetMenu(menuName = "Project/Items/Tool Item")]
    public class ToolItem : Item
    {
        // 兜底值 ✓（真正生效的是每个 ToolItem 资产里的 maxDurability / durability ✓）：
        // 仪器 10 点 ✓、工具包 5 点 ✓ —— 见 Docs/ContainmentRules.md §2 与 §5.3-1 ✓。
        [SerializeField] private int maxDurability = 10;
        [SerializeField] private int durability = 10;
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
