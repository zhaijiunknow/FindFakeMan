using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Interactables;
using UnityEngine;

namespace Project.Gameplay.Scripts.Items
{
    /// <summary>
    /// 方向动作的执行器：把「玩家往某个方向拖」翻译成对 Manager 的调用。
    ///
    /// 对应设计表 §3.3 的那张映射表（PickupAction → InventoryManager + EvidenceManager、
    /// DiscardAction → SanityManager、EquipAction → InventoryManager + UIManager、InspectAction → UIManager）。
    ///
    /// ⚠️ 这里**不负责**收起面板/切状态：那是 UI 层的事（UIManager.HideInspector → ISceneUiView.HideInspector
    /// 里统一把 Inspection 退回 Exploration）。
    /// </summary>
    public static class ItemActionRunner
    {
        /// <summary>这个动作现在能不能做（不发任何提示、不改状态，用来决定要不要变灰/提示）。</summary>
        public static bool CanRun(ItemActionKind kind, Item item, SimpleInteractable interactable)
        {
            switch (kind)
            {
                case ItemActionKind.Pickup:
                    return interactable != null && interactable.IsActive && item != null;
                case ItemActionKind.Discard:
                    return item != null;
                case ItemActionKind.Inspect:
                    return interactable != null || item != null;
                case ItemActionKind.Equip:
                    return item is ToolItem;
                default:
                    return false;
            }
        }

        /// <summary>执行动作。返回是否成功（失败时会自己给提示）。</summary>
        public static bool Run(ItemActionKind kind, Item item, SimpleInteractable interactable)
        {
            switch (kind)
            {
                case ItemActionKind.Pickup:
                    return Pickup(item, interactable);
                case ItemActionKind.Discard:
                    return Discard(item, interactable);
                case ItemActionKind.Inspect:
                    return Inspect(item, interactable);
                case ItemActionKind.Equip:
                    return Equip(item);
                default:
                    return false;
            }
        }

        // ---- 拾取：线索 → 收容箱/背包 + 证据 ----

        private static bool Pickup(Item item, SimpleInteractable interactable)
        {
            if (interactable == null || !interactable.IsActive || item == null)
            {
                ShowHint("这里没有可以拾取的东西。", true);
                return false;
            }

            if (!Services.TryGet<InventoryManager>(out var inventory))
            {
                ShowHint("背包系统没就绪。", true);
                return false;
            }

            if (item is ClueItem clue)
            {
                // 异常线索（requiresContainment）进收容箱，其余进背包 —— 设计表 §2.2。
                var toContainment = clue.RequiresContainment || clue.IsAnomaly;
                if (toContainment && inventory.IsContainmentFull())
                {
                    ShowHint("收容箱满了，先处理掉一些东西。", true);
                    return false;
                }

                if (!(toContainment ? inventory.AddToContainment(clue) : inventory.AddToInventory(clue)))
                {
                    ShowHint("收容失败。", true);
                    return false;
                }

                if (Services.TryGet<EvidenceManager>(out var evidence))
                {
                    evidence.OnItemCollected(clue);
                }

                ShowHint($"已收容：{clue.DisplayName}", false);
            }
            else
            {
                if (!inventory.AddToInventory(item))
                {
                    ShowHint("背包满了。", true);
                    return false;
                }

                ShowHint($"已拾取：{item.DisplayName}", false);
            }

            interactable.SetCollected();
            PlaySfx("item_pickup");
            return true;
        }

        // ---- 丢弃：异常线索扣 SAN（设计表 §2.2）----

        private static bool Discard(Item item, SimpleInteractable interactable)
        {
            if (item == null)
            {
                ShowHint("手上没有东西可以丢。", true);
                return false;
            }

            if (Services.TryGet<InventoryManager>(out var inventory))
            {
                if (!inventory.RemoveItem(item.ItemId) && !(item is ClueItem))
                {
                    ShowHint($"{item.DisplayName} 不在身上。", true);
                    return false;
                }
            }

            if (item is ClueItem clue && clue.IsAnomaly && Services.TryGet<SanityManager>(out var sanity))
            {
                sanity.ReduceSanity(2);
                ShowHint($"丢掉异常线索让你更不安了（SAN -2）：{clue.DisplayName}", true);
            }
            else
            {
                ShowHint($"已丢弃：{item.DisplayName}", false);
            }

            return true;
        }

        // ---- 检视：只报描述，不改状态 ----

        private static bool Inspect(Item item, SimpleInteractable interactable)
        {
            var text = interactable != null ? interactable.Description : null;
            if (string.IsNullOrWhiteSpace(text) && item != null)
            {
                text = item.Description;
            }

            ShowHint(string.IsNullOrWhiteSpace(text) ? "没什么特别的。" : text, false);
            return true;
        }

        // ---- 装备：工具进装备栏 ----

        private static bool Equip(Item item)
        {
            if (!(item is ToolItem tool))
            {
                ShowHint("只有工具能装备。", true);
                return false;
            }

            if (!Services.TryGet<InventoryManager>(out var inventory))
            {
                ShowHint("背包系统没就绪。", true);
                return false;
            }

            // 找一个空槽（装备栏容量 3）。
            for (var slot = 0; slot < inventory.EquipmentCapacity; slot++)
            {
                if (inventory.GetEquippedTool(slot) == null)
                {
                    inventory.EquipTool(tool, slot);
                    ShowHint($"已装备：{tool.DisplayName}（槽位 {slot + 1}）", false);
                    return true;
                }
            }

            ShowHint("装备栏满了。", true);
            return false;
        }

        private static void ShowHint(string text, bool warn)
        {
            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.ShowHint(text, 2f);
                if (warn)
                {
                    uiManager.ShowToolResult(text, true);
                }
            }
            else
            {
                Debug.Log($"[ItemAction] {text}");
            }
        }

        private static void PlaySfx(string sfxId)
        {
            if (Services.TryGet<AudioManager>(out var audioManager))
            {
                audioManager.PlaySFX(sfxId, 1f);
            }
        }
    }
}
