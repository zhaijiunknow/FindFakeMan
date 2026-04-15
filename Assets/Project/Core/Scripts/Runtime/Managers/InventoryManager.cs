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

        private readonly List<string> inventoryItemIds = new();
        private readonly List<string> containmentItemIds = new();
        private readonly List<string> equippedToolIds = new();

        public int InventoryCapacity => inventoryCapacity;
        public int ContainmentCapacity => containmentCapacity;
        public int EquipmentCapacity => equipmentCapacity;

        public async UniTask Initialize()
        {
            inventoryItemIds.Clear();
            containmentItemIds.Clear();
            equippedToolIds.Clear();
            await UniTask.Yield();
        }

        public bool AddToInventory(Item item)
        {
            if (item == null || inventoryItemIds.Count >= inventoryCapacity) return false;
            inventoryItemIds.Add(item.ItemId);
            return true;
        }

        public bool AddToContainment(Item item)
        {
            if (item == null || containmentItemIds.Count >= containmentCapacity) return false;
            containmentItemIds.Add(item.ItemId);
            Services.TryGet<UIManager>(out var uiManager);
            uiManager?.UpdateContainmentDisplay(containmentItemIds.Count, containmentCapacity);
            return true;
        }

        public bool RemoveItem(string itemId)
        {
            var removed = inventoryItemIds.Remove(itemId) || containmentItemIds.Remove(itemId) || equippedToolIds.Remove(itemId);
            if (removed)
            {
                Services.TryGet<UIManager>(out var uiManager);
                uiManager?.UpdateContainmentDisplay(containmentItemIds.Count, containmentCapacity);
                uiManager?.UpdateEquipmentSlots();
            }
            return removed;
        }

        public bool IsInventoryFull() => inventoryItemIds.Count >= inventoryCapacity;
        public bool IsContainmentFull() => containmentItemIds.Count >= containmentCapacity;
        public bool IsInContainment(string itemId) => containmentItemIds.Contains(itemId);
        public bool ContainsItem(string itemId) => inventoryItemIds.Contains(itemId) || containmentItemIds.Contains(itemId) || equippedToolIds.Contains(itemId);
        public IReadOnlyList<string> GetInventoryItemIds() => inventoryItemIds;
        public IReadOnlyList<string> GetContainmentItemIds() => containmentItemIds;
        public IReadOnlyList<string> GetEquippedToolIds() => equippedToolIds;

        public bool EquipTool(ToolItem toolItem, int slotIndex)
        {
            if (toolItem == null || slotIndex < 0 || slotIndex >= equipmentCapacity) return false;
            while (equippedToolIds.Count <= slotIndex)
            {
                equippedToolIds.Add(string.Empty);
            }
            equippedToolIds[slotIndex] = toolItem.ItemId;
            Services.TryGet<UIManager>(out var uiManager);
            uiManager?.UpdateEquipmentSlots();
            return true;
        }

        public string GetEquippedToolId(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < equippedToolIds.Count ? equippedToolIds[slotIndex] : string.Empty;
        }

        public InventorySaveData GetSaveData()
        {
            return new InventorySaveData
            {
                inventoryItemIds = new List<string>(inventoryItemIds),
                containmentItemIds = new List<string>(containmentItemIds),
                equippedToolIds = new List<string>(equippedToolIds)
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
            inventoryItemIds.Clear();
            containmentItemIds.Clear();
            equippedToolIds.Clear();
            if (data == null) return;
            inventoryItemIds.AddRange(data.inventoryItemIds ?? Enumerable.Empty<string>());
            containmentItemIds.AddRange(data.containmentItemIds ?? Enumerable.Empty<string>());
            equippedToolIds.AddRange(data.equippedToolIds ?? Enumerable.Empty<string>());
        }
    }
}
