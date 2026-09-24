using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Case;
using Project.Gameplay.Scripts.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 只做一件事：把工具图标填进窗口底部的 4 个工具槽（`BigApp/item/boxcontent/itemButtonN/item`）。
    ///
    /// 为什么要一个"轻量版"：关卡的 <see cref="InvestigationHudView"/> 是建造工具加在**关卡那份**
    /// `Main.prefab` 实例上的，而序幕用的是另一份实例 —— 序幕里没有 `InventoryManager`，
    /// 也不需要 HUD 那一整套（SAN/证据/详情区）。所以这里只填图标，别的什么都不管。
    ///
    /// 显示时机：如果连了 <see cref="TerminalBootTransition"/>，就等它演完（自检 / 全屏都结束）再填；
    /// 没连就立刻填。填完就不管了 —— 图标在窗口里，窗口全屏时自然跟着一起放大。
    ///
    /// 装备来源：优先用这里手填的 <see cref="tools"/>（序幕用这个）；
    /// 没填就退回 `InventoryManager.GetEquippedTools()`（关卡那边如果要复用也顺手支持）。
    /// </summary>
    public sealed class ToolSlotIconsView : MonoBehaviour
    {
        [Header("槽位")]
        [Tooltip("4 个槽位里的图标 Image，按槽位顺序（…/itemButton1/item … itemButton4/item）。")]
        [SerializeField] private Image[] slotIcons = new Image[0];

        [Header("装备")]
        [Tooltip("要显示的工具（序幕这里手填 4 件；留空则读 InventoryManager 的装备）。")]
        [SerializeField] private ToolItem[] tools = new ToolItem[0];

        [Header("时机")]
        [Tooltip("连上序幕的过场：等它演完（自检 → 全屏）再填。留空 = 一进来就填。")]
        [SerializeField] private TerminalBootTransition bootTransition;
        [Tooltip("留空时是否也接受「已经演完」的判定：过场不演（直接展开）也算演完。")]
        [SerializeField] private bool fillWhenBootMissing = true;

        [Header("交给关卡（序幕 → 关卡）")]
        [Tooltip("填完把这 4 件 + 本局种子写进交接单：这样序幕 UI 上显示的就是关卡里实际带的。")]
        [SerializeField] private bool publishToLevel = true;
        [Tooltip("交给关卡的本局种子（序幕定，关卡照办 = 这一关是固定的）。")]
        [SerializeField] private int caseSeed = 2050;

        private bool filled;
        private bool logged;

        private void OnEnable()
        {
            // 序幕里 MainUI 一直是被 SetActive(false) 藏着的：
            // 挂在 OnEnable 上，窗口一露出来（和自检文字同一帧）图标就已经在了，不会晚一帧。
            TryFill();
        }

        private void Update()
        {
            TryFill();
        }

        private void TryFill()
        {
            if (filled)
            {
                return;
            }

            if (bootTransition != null && !bootTransition.IsFinished)
            {
                return;
            }

            if (bootTransition == null && !fillWhenBootMissing)
            {
                return;
            }

            filled = true;
            Fill();
        }

        /// <summary>立刻填一遍（也可以从别处调，比如换装备之后）。</summary>
        [ContextMenu("重新填充工具槽")]
        public void Fill()
        {
            if (slotIcons == null || slotIcons.Length == 0)
            {
                Debug.LogWarning($"[ToolSlots] {name} 没有连槽位图标，工具槽不会显示东西。");
                return;
            }

            var source = ResolveTools();
            for (var i = 0; i < slotIcons.Length; i++)
            {
                var icon = slotIcons[i];
                if (icon == null)
                {
                    continue;
                }

                var tool = source != null && i < source.Length ? source[i] : null;
                var sprite = tool != null ? tool.Icon : null;

                icon.sprite = sprite;
                // 和关卡 HUD 的口径一致：没有图标就不显示这个槽位的图（底框还在，只是空槽）。
                icon.gameObject.SetActive(sprite != null);
                icon.preserveAspect = true;
            }

            if (!logged)
            {
                logged = true;
                Debug.Log($"[ToolSlots] {name} 已填充 {CountFilled(source)} 个工具图标。");
            }

            // 把"这一关固定 + 带这几件"交给关卡：序幕 UI 上显示的就是关卡里实际会带的，
            // 不会再出现"序幕显示 4 件、进关卡换成另外 4 件"。
            if (publishToLevel && tools != null && tools.Length > 0)
            {
                CaseHandoff.Publish(caseSeed, tools);
            }
        }

        private Item[] ResolveTools()
        {
            if (tools != null && tools.Length > 0)
            {
                return tools;
            }

            if (Services.TryGet<InventoryManager>(out var inventoryManager))
            {
                var equipped = inventoryManager.GetEquippedTools();
                if (equipped != null && equipped.Count > 0)
                {
                    var list = new Item[equipped.Count];
                    for (var i = 0; i < equipped.Count; i++)
                    {
                        list[i] = equipped[i];
                    }

                    return list;
                }
            }

            return null;
        }

        private static int CountFilled(Item[] source)
        {
            if (source == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var item in source)
            {
                if (item != null && item.Icon != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
