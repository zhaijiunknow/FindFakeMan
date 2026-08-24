using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Project.Core.Runtime.Framework;
using Project.Gameplay.Scripts.Items;

namespace Project.Core.Runtime.Managers
{
    public sealed class InventoryManager : ManagerBehaviour, ISaveable<InventorySaveData>
    {
        [SerializeField] private int inventoryCapacity = 9;
        [SerializeField] private int containmentCapacity = 3;
        [SerializeField] private int equipmentCapacity = 3;

        // 直接持有 Item 引用（运行时真相）；存档时再转回 id。
        private readonly List<Item> inventoryItems = new();
        private readonly List<Item> containmentItems = new();
        private readonly List<Item> equippedTools = new();
        private IReadOnlyList<Item> itemSource = System.Array.Empty<Item>();

        public int InventoryCapacity => inventoryCapacity;
        public int ContainmentCapacity => containmentCapacity;
        public int EquipmentCapacity => equipmentCapacity;

        /// <summary>设置全局可用的 Item 列表（读档时按 itemId 解析 Item 引用）。</summary>
        public void SetItemSource(IEnumerable<Item> source)
        {
            itemSource = source?.ToArray() ?? System.Array.Empty<Item>();
        }

        public async UniTask Initialize()
        {
            inventoryItems.Clear();
            containmentItems.Clear();
            equippedTools.Clear();
            await UniTask.Yield();
        }

        public bool AddToInventory(Item item)
        {
            if (item == null || inventoryItems.Count >= inventoryCapacity) return false;
            inventoryItems.Add(item);
            return true;
        }

        public bool AddToContainment(Item item)
        {
            if (item == null || containmentItems.Count >= containmentCapacity) return false;
            containmentItems.Add(item);
            Services.TryGet<UIManager>(out var uiManager);
            uiManager?.UpdateContainmentDisplay(containmentItems.Count, containmentCapacity);
            return true;
        }

        public bool RemoveItem(string itemId)
        {
            var removed =
                inventoryItems.RemoveAll(i => i != null && i.ItemId == itemId) +
                containmentItems.RemoveAll(i => i != null && i.ItemId == itemId) +
                equippedTools.RemoveAll(i => i != null && i.ItemId == itemId);
            if (removed > 0)
            {
                Services.TryGet<UIManager>(out var uiManager);
                uiManager?.UpdateContainmentDisplay(containmentItems.Count, containmentCapacity);
                uiManager?.UpdateEquipmentSlots();
            }

            return removed > 0;
        }

        public bool IsInventoryFull() => inventoryItems.Count >= inventoryCapacity;
        public bool IsContainmentFull() => containmentItems.Count >= containmentCapacity;
        public bool IsInContainment(string itemId) => containmentItems.Any(i => i != null && i.ItemId == itemId);
        public bool ContainsItem(string itemId) =>
            inventoryItems.Concat(containmentItems).Concat(equippedTools).Any(i => i != null && i.ItemId == itemId);

        // ---------- Item 引用查询（运行时真相） ----------
        public IReadOnlyList<Item> GetInventoryItems() => inventoryItems;
        public IReadOnlyList<Item> GetContainmentItems() => containmentItems;
        public IReadOnlyList<Item> GetEquippedTools() => equippedTools;
        public Item GetEquippedTool(int slotIndex) =>
            slotIndex >= 0 && slotIndex < equippedTools.Count ? equippedTools[slotIndex] : null;

        // ---------- id 查询（存档/外部兼容） ----------
        public IReadOnlyList<string> GetInventoryItemIds() => inventoryItems.Where(i => i != null).Select(i => i.ItemId).ToList();
        public IReadOnlyList<string> GetContainmentItemIds() => containmentItems.Where(i => i != null).Select(i => i.ItemId).ToList();
        public IReadOnlyList<string> GetEquippedToolIds() => equippedTools.Where(i => i != null).Select(i => i.ItemId).ToList();
        public string GetEquippedToolId(int slotIndex) => GetEquippedTool(slotIndex)?.ItemId ?? string.Empty;

        public bool EquipTool(ToolItem toolItem, int slotIndex)
        {
            if (toolItem == null || slotIndex < 0 || slotIndex >= equipmentCapacity) return false;
            while (equippedTools.Count <= slotIndex)
            {
                equippedTools.Add(null);
            }

            equippedTools[slotIndex] = toolItem;
            Services.TryGet<UIManager>(out var uiManager);
            uiManager?.UpdateEquipmentSlots();
            return true;
        }

        // ---------- 存档 ----------

        public InventorySaveData GetSaveData()
        {
            return new InventorySaveData
            {
                inventoryItemIds = GetInventoryItemIds().ToList(),
                containmentItemIds = GetContainmentItemIds().ToList(),
                equippedToolIds = GetEquippedToolIds().ToList()
            };
        }

        public async UniTask LoadState(InventorySaveData data)
        {
            ApplyState(data);
            await UniTask.Yield();
        }

        void ISaveable<InventorySaveData>.LoadState(InventorySaveData data) => ApplyState(data);

        private void ApplyState(InventorySaveData data)
        {
            inventoryItems.Clear();
            containmentItems.Clear();
            equippedTools.Clear();
            if (data == null) return;

            inventoryItems.AddRange((data.inventoryItemIds ?? Enumerable.Empty<string>()).Select(FindItem).Where(i => i != null));
            containmentItems.AddRange((data.containmentItemIds ?? Enumerable.Empty<string>()).Select(FindItem).Where(i => i != null));
            equippedTools.AddRange((data.equippedToolIds ?? Enumerable.Empty<string>()).Select(FindItem).Where(i => i != null));
        }

        private Item FindItem(string itemId) =>
            itemSource.FirstOrDefault(i => i != null && i.ItemId == itemId);
    }
}
