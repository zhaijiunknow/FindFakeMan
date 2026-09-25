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

        // 背包 = 本局的**可用池**（道具页左列 ✓）：想换就打开道具页换 ✓，工具包只有 4 格 ✓。
        // 和 inventoryItems（随身携带的**非工具**物品 ✓，比如【李阳的手表】）分开 ✗ —— 两者语义不同 ✓。
        private readonly List<Item> backpackItems = new();
        private IReadOnlyList<Item> itemSource = System.Array.Empty<Item>();

        public int InventoryCapacity => inventoryCapacity;
        public int ContainmentCapacity => containmentCapacity;
        public int EquipmentCapacity => equipmentCapacity;

        /// <summary>设置全局可用的 Item 列表（读档时按 itemId 解析 Item 引用）。</summary>
        public void SetItemSource(IEnumerable<Item> source)
        {
            itemSource = source?.ToArray() ?? System.Array.Empty<Item>();
        }

        /// <summary>
        /// 全局 Item 列表（只读）✓。
        /// 给案情用：<c>CaseDirector</c> 开局要按 id 把那几件收容物挂到固定产出点上 ✓，
        /// 有了这个出口它就不用再在 Inspector 里连一遍引用 ✓（少一处会漏连的地方 ✓）。
        /// </summary>
        public IReadOnlyList<Item> ItemSource => itemSource;

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

        // ---------- 背包（本局可用池）↔ 工具包（随身 4 格）----------

        /// <summary>
        /// 开局把本局可用的**整池**工具放进背包 ✓ —— bootstrapper 会喂 `CaseDirector.LoadoutPool` ✓
        ///（5 件 ✓），而工具包里只装案情那一套（4 件 ✓）：于是"带哪几件进去"变成玩家的选择 ✓，
        /// 剧情里念的"5 件装备"也因此成立 ✓（见 Docs/ContainmentRules.md §6 ✓）。
        /// </summary>
        public void SetBackpackItems(IEnumerable<Item> items)
        {
            backpackItems.Clear();
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                if (item != null && !backpackItems.Contains(item))
                {
                    backpackItems.Add(item);
                }
            }
        }

        public IReadOnlyList<Item> GetBackpackItems() => backpackItems;

        /// <summary>背包 → 工具包 ✓（找空槽装 ✓）。成功时会顺手把它从背包里拿掉 ✓。</summary>
        public bool MoveToToolBag(Item item)
        {
            if (item == null)
            {
                return false;
            }

            for (var slot = 0; slot < equipmentCapacity; slot++)
            {
                if (GetEquippedTool(slot) != null)
                {
                    continue;
                }

                if (!EquipTool(item as ToolItem, slot))
                {
                    return false;
                }

                backpackItems.Remove(item);
                PushToolBagToToolInput();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 工具包 → 背包 ✓ —— **不是销毁** ✗：`RemoveItem`（丢弃那条）会把它从所有列表删干净 ✗，
        /// 而"放回去"只是回到背包 ✓，随时能再拿 ✓。
        /// </summary>
        public bool MoveToBackpack(Item item)
        {
            if (item == null || !equippedTools.Remove(item))
            {
                return false;
            }

            if (!backpackItems.Contains(item))
            {
                backpackItems.Add(item);
            }

            PushToolBagToToolInput();

            Services.TryGet<UIManager>(out var uiManager);
            uiManager?.UpdateEquipmentSlots();
            return true;
        }

        /// <summary>
        /// 把"工具包里这套"推给工具条 ✓ —— 工具条是**已装备工具的视图** ✓，
        /// 背包↔工具包换完之后不推一次的话 ✗，HUD 底部那 4 格和拖拽用的还是旧那套 ✗（看起来像"换了没用"✗）。
        /// </summary>
        private void PushToolBagToToolInput()
        {
            if (!Services.TryGet<IToolInputService>(out var toolInput))
            {
                return;
            }

            var belt = new List<ToolItem>();
            for (var slot = 0; slot < equipmentCapacity; slot++)
            {
                if (GetEquippedTool(slot) is ToolItem tool)
                {
                    belt.Add(tool);
                }
            }

            toolInput.SetTools(belt);
        }

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
