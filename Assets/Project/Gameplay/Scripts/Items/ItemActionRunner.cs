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
                    // **有选中家具就行** ✓（不要求 `hud.CurrentItem` 正好是那件东西 ✓ ——
                    // 面板没开时它是 null ✓，但「这件家具身上有东西」仍然成立 ✓。兜底见 Pickup ✓）。
                    return interactable != null && interactable.IsActive
                           && (item != null || interactable.AssociatedItem != null);
                case ItemActionKind.Discard:
                    // **只有"直接选中收容箱里的东西"才能丢** ✓ —— 判定就是 `interactable == null`：
                    // 收容格 / 工具槽走的是 `ShowInspector(item, null)` ✓，家具走的是 `(item, interactable)` ✓。
                    //
                    // 为什么必须这么判 ✗→✓：选中家具时 `hud.CurrentItem` 可能是**刚从它身上收进箱子的那件收容物** ✓，
                    // 而它此时**确实在箱子里** ✗ → 只判"在不在箱子里"就会让家具也亮出「↓丢弃」✗，
                    // 一下就能把刚收走的东西扔掉 ✗（玩家此刻的意图明明是"看这件家具"✓）。
                    // 工具槽里的工具也不满足 ✓（它们不在箱子里 ✓，要放回背包走「←装备」那个开关 ✓）。
                    return interactable == null && item != null && IsItemInContainment(item);
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
            if (interactable == null || !interactable.IsActive)
            {
                ShowHint("这里没有可以拾取的东西。", true);
                return false;
            }

            // 谁说了算 ✓：**交互物自己的东西优先** ✓ ——
            // 关了面板之后 `hud.CurrentItem` 会**留着上一次的东西** ✓（选中状态要持续 ✓），
            // 那件东西可能已经被拾取走了 ✗；而交互物身上的 `AssociatedItem` 在拾取时已被清空 ✓，
            // 所以以它为准就不会"同一件收容物被重复收两次"✗。没有规则的老交互物才退回用传进来的 item ✓。
            if (interactable.GetComponent<SampleInteractableRule>() != null)
            {
                item = interactable.AssociatedItem;
            }
            else
            {
                item ??= interactable.AssociatedItem;
            }

            if (item == null)
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
                // 进不进收容箱**问产地规则** ✓ —— 见 Docs/ContainmentRules.md §1：
                // 异常收容物和正常收容物**都能收容** ✓，所以不能按 clue.IsAnomaly 判 ✗
                //（那会把正常收容物塞进背包 ✗）。只有场景里烘死规则的老交互物才退回老判断 ✓。
                var rule = interactable.GetComponent<SampleInteractableRule>();
                var toContainment = rule != null
                    ? rule.CollectsToContainment
                    : clue.RequiresContainment || clue.IsAnomaly;
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

            // 拿过就**再也不给** ✓ —— 丢弃之后同样找不回来 ✓（把产地手上那件清掉 ✓）。
            interactable.GetComponent<SampleInteractableRule>()?.MarkClueTaken();

            PlaySfx("item_pickup");
            return true;
        }

        /// <summary>
        /// 这件东西现在是不是在**收容箱**里 ✓ —— 丢弃的资格就等于「它在箱子里」✓。
        /// 工具槽里那些**不在**箱子里 ✓，所以它们不会亮出「↓丢弃」✓（它们走「←装备」那个开关 ✓）。
        /// </summary>
        private static bool IsItemInContainment(Item item)
        {
            return item != null
                   && Services.TryGet<InventoryManager>(out var inventory)
                   && inventory.IsInContainment(item.ItemId);
        }

        // ---- 丢弃：异常线索扣 SAN（设计表 §2.2）----

        private static bool Discard(Item item, SimpleInteractable interactable)
        {
            if (item == null)
            {
                ShowHint("手上没有东西可以丢。", true);
                return false;
            }

            // 只有**收容箱里**的东西能丢 ✓ —— 而且这条不只是"体验"✓：
            // 下面那句 `RemoveItem` 会把物品从**所有**列表里删掉 ✗，
            // 万一是从工具槽走过来的 ✓，工具（含耐久）会被直接销毁 ✗。所以这里必须挡住 ✓。
            if (!IsItemInContainment(item))
            {
                ShowHint($"{item.DisplayName} 不在收容箱里 —— 只有收容物能丢弃。", true);
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

        // ---- 装备：背包 ↔ 工具包的**开关**（同一个方向动作，两边都能调 ✓）----

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

            // 已经在工具包里 → 这一次「装备」的意思就是**放回背包** ✓（**不是销毁** ✗）。
            // 做成开关的理由 ✓：背包↔工具包只需要一个方向动作 ✓，
            // 道具页每行一个按钮、HUD 里左拖一下，调的也都是这同一个 toggle ✓。
            for (var slot = 0; slot < inventory.EquipmentCapacity; slot++)
            {
                if (!ReferenceEquals(inventory.GetEquippedTool(slot), tool))
                {
                    continue;
                }

                if (inventory.MoveToBackpack(tool))
                {
                    ShowHint($"已放回背包：{tool.DisplayName} ✓", false);
                    return true;
                }

                ShowHint("放不回背包。", true);
                return false;
            }

            // 不在工具包里 → 从背包装上来 ✓（`MoveToToolBag` 会顺手把它从背包里拿掉 ✓）。
            if (inventory.MoveToToolBag(tool))
            {
                ShowHint($"已装进工具包：{tool.DisplayName} ✓", false);
                return true;
            }

            ShowHint("工具包满了 —— 先放回去一件，或者直接换另一件。", true);
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
