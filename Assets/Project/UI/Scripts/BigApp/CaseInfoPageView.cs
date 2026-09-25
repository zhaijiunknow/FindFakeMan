using System.Text;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Case;
using Project.Gameplay.Scripts.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>这一页显示什么（一个组件跑几种页，省得写几个几乎一样的类）。</summary>
    public enum CasePageMode
    {
        /// <summary>线索清单：所有家具 + 已查/未查（恐怖症那种勾选清单）。</summary>
        Checklist = 0,

        /// <summary>道具：本局带的工具，图标名 / 耐久 / 说明。</summary>
        Tools = 1,

        /// <summary>案件：案号 / 本局种子 / 进度 / 目标身份未知。</summary>
        CaseInfo = 2,

        /// <summary>收容：格子里装了什么（`InventoryManager.GetContainmentItems()`）。</summary>
        Containment = 3,
    }

    /// <summary>
    /// 小软件里的只读信息页（线索清单 / 道具 / 案件 / 收容）：读一个 manager，排成几行。
    ///
    /// 和 <see cref="CaseJournalView"/> 一样是两段式（为了控件能烘进场景 ✓）：
    /// <see cref="Build"/> 建控件（运行时或编辑期 ✓）、<see cref="WireEvents"/> 接点击（Awake 里统一做 ✓）。
    /// </summary>
    public sealed class CaseInfoPageView : MonoBehaviour
    {
        [Header("内容")]
        [SerializeField] private CasePageMode mode = CasePageMode.Checklist;

        [Header("行为")]
        [Tooltip("自己搭版式。建造工具烘场景时会设成 false ✓。")]
        [SerializeField] private bool buildAtRuntime = true;
        [Tooltip("标题文字；留空则按 mode 自动取（线索清单 / 道具 / 案件 / 收容）。")]
        [SerializeField] private string pageTitle = string.Empty;

        [Header("控件引用（建造工具烘场景时写入 ✓）")]
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private Button backButton;

        private CaseDirector director;
        private int lastSignature = -1;
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
            if (director == null && !Services.TryGet<CaseDirector>(out director))
            {
                return;
            }

            // 手变即刷新：用一个签名（读数/异常/目标）判断要不要重排文本。
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

        /// <summary>建控件（不接点击 ✗）。</summary>
        public void Build()
        {
            var font = SmallAppPageStyle.ResolveFont(this);
            SmallAppPageStyle.Title(transform, font, string.IsNullOrEmpty(pageTitle) ? TitleOf(mode) : pageTitle);
            bodyText = SmallAppPageStyle.Body(transform, font, string.Empty);
            backButton = SmallAppPageStyle.BackButton(transform, font, null);
        }

        /// <summary>接点击（幂等 ✓）。</summary>
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

        private static string TitleOf(CasePageMode pageMode)
        {
            switch (pageMode)
            {
                case CasePageMode.Checklist: return "线索清单";
                case CasePageMode.Tools: return "道具";
                case CasePageMode.CaseInfo: return "案件";
                default: return "收容";
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
                case CasePageMode.Containment:
                    return BuildContainment();
                default:
                    return BuildCaseInfo();
            }
        }

        // ---------- 各种页 ----------

        private string BuildChecklist()
        {
            var text = new StringBuilder();
            text.AppendLine("查过的东西会留在这里 —— 没查的还不知道有什么。");
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
            text.AppendLine("这一局带进来的东西。");
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

        private string BuildContainment()
        {
            var text = new StringBuilder();

            if (!Services.TryGet<InventoryManager>(out var inventory))
            {
                text.AppendLine("（没有 InventoryManager）");
                return text.ToString();
            }

            var capacity = inventory.ContainmentCapacity;
            var contained = inventory.GetContainmentItems();
            var count = contained != null ? contained.Count : 0;

            text.AppendLine($"收容箱　{count}/{capacity}");
            text.AppendLine();

            // ---- 产地清单：**只在我们调试时显示** ✓ ----
            // 默认**不列** ✗：把"哪件家具有收容物"写出来 = 直接泄底 ✗（这一局该翻哪儿，
            // 本来就该靠读数自己找 ✓）。要调试就去 `CaseDirector` 上勾 `debugShowClueSites` ✓，
            // 或者直接看 Console 里那行 `[Case] 本局收容物产地 …：obj_3bXXX` ✓。
            var sites = director != null ? director.ClueSites : null;
            if (director != null && director.DebugShowClueSites && sites != null && sites.Count > 0)
            {
                text.AppendLine("【调试】本局可收容物：");
                foreach (var site in sites)
                {
                    var mark = inventory.IsInContainment(site.ClueId) ? "✔" : "·";
                    text.AppendLine($"{mark} {site.ClueName}（在 {CleanName(site.Host)}）");
                }

                text.AppendLine();
            }

            if (count == 0)
            {
                text.AppendLine("收容箱还是空的。");
                text.AppendLine("箱子只装「伪人物品」：用对口道具把这一层逐处读过去，");
                text.AppendLine("读到不对劲的地方，东西就会自己露出来。");
                return text.ToString();
            }

            text.AppendLine("收容箱里：");
            for (var i = 0; i < contained.Count; i++)
            {
                var item = contained[i];
                if (item == null)
                {
                    continue;
                }

                text.AppendLine($"· {item.DisplayName}");
            }

            return text.ToString();
        }

        private string BuildCaseInfo()
        {
            var text = new StringBuilder();
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
    }
}
