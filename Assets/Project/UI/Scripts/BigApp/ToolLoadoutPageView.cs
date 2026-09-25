using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 小软件「道具」页 = **背包 ↔ 工具包** 的配置台 ✓。
    ///
    /// 口径（见 `Docs/ContainmentRules.md` §6）：
    ///  - **背包** = 本局可用的**整池** ✓（`InventoryManager.GetBackpackItems` ✓）—— 就是装备清单里念的那几件 ✓；
    ///  - **工具包** = 随身能带的那 4 格 ✓（`EquipmentCapacity` ✓）—— **带哪几件进去是玩家的选择** ✓；
    ///  - 左列每行点一下 = 装进工具包 ✓，右列每行点一下 = 放回背包 ✓，
    ///    和 HUD 里「左拖 = 装备」是**同一个开关** ✓（`MoveToToolBag` / `MoveToBackpack` ✓）。
    ///
    /// 两段式，和别的页一样 ✓：`Build()` 建控件（编辑期由建造工具调 ✓）、`WireEvents()` 接点击 ✓。
    /// 行按钮自己的点击由 <see cref="ToolLoadoutRow"/> 在 Awake 里接 ✓（lambda 没法 Remove ✗）。
    /// </summary>
    public sealed class ToolLoadoutPageView : MonoBehaviour
    {
        /// <summary>背包最多显示几行 ✓（池子 5 件 ✓，留一格余量 ✓）。</summary>
        private const int BackpackRows = 6;

        /// <summary>工具包最多显示几行 ✓（4 格 ✓）。</summary>
        private const int ToolBagRows = 4;

        private const float RowHeight = 0.075f;
        private const float RowTop = 0.68f;
        private const float RowGap = 0.088f;

        [Header("行为")]
        [Tooltip("自己搭版式。建造工具烘场景时会设成 false ✓（控件已经在场景里了）。")]
        [SerializeField] private bool buildAtRuntime = true;

        [Header("控件引用（建造工具烘场景时写入 ✓）")]
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private TextMeshProUGUI backpackHeader;
        [SerializeField] private TextMeshProUGUI toolBagHeader;
        [SerializeField] private Button[] backpackRows = new Button[BackpackRows];
        [SerializeField] private Button[] toolBagRows = new Button[ToolBagRows];
        [SerializeField] private Button backButton;

        private InventoryManager inventory;
        private string lastSignature = string.Empty;
        private string statusMessage = string.Empty;
        private float statusUntil;
        private bool wired;

        private void Awake()
        {
            if (buildAtRuntime)
            {
                Build();
            }

            WireEvents();
        }

        private void Update()
        {
            if (inventory == null && !Services.TryGet<InventoryManager>(out inventory))
            {
                return;
            }

            // 手变即刷新：签名（背包/工具包两串 id ✓）没变就不重排 ✓。
            var signature = Signature();
            if (signature == lastSignature && Time.unscaledTime > statusUntil)
            {
                return;
            }

            lastSignature = signature;
            RefreshRows();
        }

        // ---------- 建控件（编辑期或运行时 ✓） ----------

        /// <summary>建控件 ✓。**不接点击** ✗ —— 那是 WireEvents / 行组件的事 ✓。</summary>
        public void Build()
        {
            var font = SmallAppPageStyle.ResolveFont(this);

            SmallAppPageStyle.Title(transform, font, "道具");

            hintText = SmallAppPageStyle.Body(transform, font, string.Empty);
            hintText.rectTransform.anchorMin = new Vector2(0.05f, 0.775f);
            hintText.rectTransform.anchorMax = new Vector2(0.95f, 0.85f);
            hintText.fontSize = 18;

            // 左列背包、右列工具包 ✓ —— 两列并排就是"带哪几件进去"这件事本身 ✓，不用额外说明 ✓。
            backpackHeader = SmallAppPageStyle.Caption(transform, "BackpackHeader", "背包", font, 0.70f, 0.77f, 0.05f, 0.48f);
            toolBagHeader = SmallAppPageStyle.Caption(transform, "ToolBagHeader", "工具包", font, 0.70f, 0.77f, 0.52f, 0.95f);

            backpackRows = new Button[BackpackRows];
            for (var i = 0; i < BackpackRows; i++)
            {
                backpackRows[i] = MakeRow($"BackpackRow{i}", font, 0.05f, 0.48f, i, true);
            }

            toolBagRows = new Button[ToolBagRows];
            for (var i = 0; i < ToolBagRows; i++)
            {
                toolBagRows[i] = MakeRow($"ToolBagRow{i}", font, 0.52f, 0.95f, i, false);
            }

            backButton = SmallAppPageStyle.BackButton(transform, font, null);
        }

        private Button MakeRow(string name, TMP_FontAsset font, float xMin, float xMax, int index, bool fromBackpack)
        {
            var yMin = RowTop - index * RowGap;
            var button = SmallAppPageStyle.Choice(transform, name, string.Empty, font, xMin, xMax, yMin, yMin + RowHeight);

            // 文字改成左对齐、往右让出图标的位置 ✓（Choice 默认是居中的 ✗）。
            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.Left;
                label.fontSize = 20;
                label.rectTransform.offsetMin = new Vector2(46f, 0f);
                label.rectTransform.offsetMax = new Vector2(-8f, 0f);
            }

            // 图标：和物品资产上的 icon 一致 ✓（工具槽 / 收容格也是这么显示的 ✓）。
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(button.transform, false);
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(8f, 0f);
            iconRect.sizeDelta = new Vector2(32f, 32f);
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var row = button.gameObject.AddComponent<ToolLoadoutRow>();
            row.Configure(fromBackpack, index);
            return button;
        }

        /// <summary>接点击（幂等 ✓）。行自己的点击在 <see cref="ToolLoadoutRow"/> 里 ✓。</summary>
        public void WireEvents()
        {
            if (wired)
            {
                return;
            }

            wired = true;

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(BackToMenu);
                backButton.onClick.AddListener(BackToMenu);
            }
        }

        private void BackToMenu()
        {
            var host = GetComponentInParent<SmallAppPageHost>(true);
            if (host != null)
            {
                host.BackToMenu();
            }
        }

        // ---------- 背包 ↔ 工具包（行按钮和 HUD 左拖都调这两个 ✓） ----------

        /// <summary>背包里第 <paramref name="index"/> 件 → 工具包 ✓。</summary>
        public void EquipFromBackpack(int index)
        {
            if (inventory == null && !Services.TryGet<InventoryManager>(out inventory))
            {
                return;
            }

            var backpack = inventory.GetBackpackItems();
            if (backpack == null || index < 0 || index >= backpack.Count || backpack[index] == null)
            {
                return;
            }

            var item = backpack[index];
            if (inventory.MoveToToolBag(item))
            {
                SetStatus($"已装进工具包：{item.DisplayName} ✓");
            }
            else
            {
                SetStatus("工具包满了 —— 先放回去一件，或者直接换另一件。");
            }

            lastSignature = string.Empty; // 强制下一帧重排 ✓
        }

        /// <summary>工具包里第 <paramref name="index"/> 件 → 背包 ✓（**不是丢掉** ✗，随时能再拿 ✓）。</summary>
        public void UnequipToBackpack(int index)
        {
            if (inventory == null && !Services.TryGet<InventoryManager>(out inventory))
            {
                return;
            }

            var toolBag = inventory.GetEquippedTools();
            if (toolBag == null || index < 0 || index >= toolBag.Count || toolBag[index] == null)
            {
                return;
            }

            var item = toolBag[index];
            SetStatus(inventory.MoveToBackpack(item)
                ? $"已放回背包：{item.DisplayName} ✓"
                : $"放不回背包：{item.DisplayName}");

            lastSignature = string.Empty;
        }

        // ---------- 刷新 ----------

        private void SetStatus(string message)
        {
            statusMessage = message ?? string.Empty;
            statusUntil = Time.unscaledTime + 2.5f;
        }

        private void RefreshRows()
        {
            if (inventory == null)
            {
                return;
            }

            var backpack = inventory.GetBackpackItems();
            var toolBag = inventory.GetEquippedTools();
            var capacity = inventory.EquipmentCapacity;
            var toolBagFull = toolBag.Count >= capacity;

            if (backpackHeader != null)
            {
                backpackHeader.text = $"背包（{backpack.Count} 件可用）";
            }

            if (toolBagHeader != null)
            {
                toolBagHeader.text = $"工具包（{toolBag.Count}/{capacity} 格）";
            }

            if (hintText != null)
            {
                hintText.text = Time.unscaledTime <= statusUntil && !string.IsNullOrEmpty(statusMessage)
                    ? statusMessage
                    : "左边是背包、右边是工具包 —— 点一下就能换 ✓（和 HUD 里左拖是同一件事 ✓）";
            }

            Fill(backpackRows, backpack, "装备", toolBagFull);
            Fill(toolBagRows, toolBag, "放回", false);
        }

        private static void Fill(Button[] rows, System.Collections.Generic.IReadOnlyList<Item> items, string action, bool lockWhenFull)
        {
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    continue;
                }

                var item = items != null && i < items.Count ? items[i] : null;
                var tool = item as ToolItem;

                row.gameObject.SetActive(tool != null);
                row.interactable = tool != null && !lockWhenFull;
                if (tool == null)
                {
                    continue;
                }

                var label = row.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.text = lockWhenFull ? $"{tool.DisplayName}　（满）" : $"{tool.DisplayName}　{action}";
                }

                var icon = row.transform.Find("Icon");
                var image = icon != null ? icon.GetComponent<Image>() : null;
                if (image != null)
                {
                    image.sprite = tool.Icon;
                    image.enabled = tool.Icon != null;
                }
            }
        }

        private string Signature()
        {
            if (inventory == null)
            {
                return string.Empty;
            }

            var text = new System.Text.StringBuilder();
            foreach (var item in inventory.GetBackpackItems())
            {
                text.Append(item != null ? item.ItemId : "-").Append('|');
            }

            text.Append("//");
            foreach (var item in inventory.GetEquippedTools())
            {
                text.Append(item != null ? item.ItemId : "-").Append('|');
            }

            return text.ToString();
        }
    }
}
