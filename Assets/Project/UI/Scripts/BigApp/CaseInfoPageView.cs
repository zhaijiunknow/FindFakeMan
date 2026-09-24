using System.Text;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Case;
using Project.Gameplay.Scripts.Items;
using TMPro;
using UnityEngine;

namespace Project.UI.BigApp
{
    /// <summary>这一页显示什么（一个组件跑三种页，省得写三个几乎一样的类）。</summary>
    public enum CasePageMode
    {
        /// <summary>线索清单：所有家具 + 已查/未查（恐怖症那种勾选清单）。</summary>
        Checklist = 0,

        /// <summary>道具：本局带的 4 件工具，图标 / 耐久 / 说明。</summary>
        Tools = 1,

        /// <summary>案件：案号 / 本局种子 / 进度 / 目标身份未知。</summary>
        CaseInfo = 2,
    }

    /// <summary>
    /// 小软件里的"信息页"（线索清单 / 道具 / 案件）：只读地把一个 manager 的数据排成文本。
    ///
    /// 和笔记页的分工：笔记页（<see cref="CaseJournalView"/>）是**要操作**的（勾结论 ✓），
    /// 这里三页纯看 ✓；版式也很朴素 —— 先用一个 TMP 多行文本铺满页面 ✓，
    /// 以后再换成预制体里 `colloctitem` 那种条目版式（那需要每行一个对象，属于精修 ✓）。
    /// </summary>
    public sealed class CaseInfoPageView : MonoBehaviour
    {
        [Header("内容")]
        [SerializeField] private CasePageMode mode = CasePageMode.Checklist;

        [Header("引用（留空则自己搭一个铺满的文本）")]
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private bool buildAtRuntime = true;

        private CaseDirector director;
        private int lastSignature = -1;

        private void Awake()
        {
            if (buildAtRuntime && bodyText == null)
            {
                BuildText();
            }
        }

        private void Update()
        {
            if (director == null && !Services.TryGet<CaseDirector>(out director))
            {
                return;
            }

            // 简单的手变即刷新：用一个签名（读数/异常/装备数）判断要不要重排文本。
            var signature = director.ReadingCount * 31 + director.FoundAnomalies * 7 + director.CorroborationNeeded;
            if (signature == lastSignature)
            {
                return;
            }

            lastSignature = signature;
            if (bodyText != null)
            {
                bodyText.text = BuildTextContent();
            }
        }

        private string BuildTextContent()
        {
            switch (mode)
            {
                case CasePageMode.Checklist:
                    return BuildChecklist();
                case CasePageMode.Tools:
                    return BuildTools();
                default:
                    return BuildCaseInfo();
            }
        }

        // ---------- 三种页 ----------

        private string BuildChecklist()
        {
            var text = new StringBuilder();
            text.AppendLine("【线索清单】查过的东西会留在这里 —— 没查的还不知道有什么。");
            text.AppendLine();

            var all = director.AllSpecs;
            if (all.Count == 0)
            {
                text.AppendLine("（案情还没生成）");
                return text.ToString();
            }

            var read = 0;
            foreach (var spec in all)
            {
                if (spec.Read)
                {
                    read++;
                }
            }

            text.AppendLine($"已查 {read}/{all.Count}　·　异常读数 {director.FoundAnomalies}/{director.CorroborationNeeded}");
            text.AppendLine();

            foreach (var spec in all)
            {
                var name = CleanName(spec.DisplayName);
                text.AppendLine(spec.Read
                    ? $"✔ {name}：{spec.SuccessText}"
                    : $"… {name}：还没查过");
            }

            return text.ToString();
        }

        private string BuildTools()
        {
            var text = new StringBuilder();
            text.AppendLine("【道具】这一局带进来的东西。");
            text.AppendLine();

            if (!Services.TryGet<InventoryManager>(out var inventory))
            {
                text.AppendLine("（没有 InventoryManager）");
                return text.ToString();
            }

            var tools = inventory.GetEquippedTools();
            if (tools == null || tools.Count == 0)
            {
                text.AppendLine("（没有装备任何工具）");
                return text.ToString();
            }

            foreach (var item in tools)
            {
                if (item == null)
                {
                    continue;
                }

                text.AppendLine($"· {item.DisplayName}");
                if (item is ToolItem tool)
                {
                    text.AppendLine($"    耐久 {tool.Durability}/{tool.MaxDurability}");
                }

                if (!string.IsNullOrWhiteSpace(item.Description))
                {
                    text.AppendLine($"    {item.Description}");
                }
            }

            return text.ToString();
        }

        private string BuildCaseInfo()
        {
            var text = new StringBuilder();
            text.AppendLine("【案件】");
            text.AppendLine();
            text.AppendLine("案号　　PX-2050-734");
            text.AppendLine("地点　　白婉别墅 · 客厅");
            text.AppendLine("身份　　A先生（OKAS 侦查员）");
            text.AppendLine($"本局种子　{director.Seed}");
            text.AppendLine();
            text.AppendLine($"观测　　{director.ReadingCount} 件家具");
            text.AppendLine($"异常读数　{director.FoundAnomalies}/{director.CorroborationNeeded}"
                            + (director.IsEnoughToConclude ? "（够互相印证）" : "（还不够）"));
            text.AppendLine();
            text.AppendLine("目标身份：未知 —— 需要自己根据读数判断。");
            text.AppendLine("结论在「笔记」页里勾。");
            return text.ToString();
        }

        private static string CleanName(string displayName)
        {
            return (displayName ?? string.Empty)
                .Replace("obj_3b", string.Empty)
                .Replace("obj_3a", string.Empty);
        }

        private void BuildText()
        {
            var go = new GameObject("PageBody", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.03f, 0.04f);
            rect.anchorMax = new Vector2(0.97f, 0.96f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            bodyText = go.GetComponent<TextMeshProUGUI>();
            bodyText.font = ResolveFont();
            bodyText.fontSize = 20;
            bodyText.color = new Color(0.88f, 0.94f, 1f, 1f);
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.raycastTarget = false;
        }

        private TMP_FontAsset ResolveFont()
        {
            var canvas = GetComponentInParent<Canvas>(true);
            var host = canvas != null ? canvas.transform : transform.root;
            foreach (var text in host.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text != null && text.font != null)
                {
                    return text.font;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }
    }
}
