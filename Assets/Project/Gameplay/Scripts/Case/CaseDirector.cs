using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;
using UnityEngine;

// UnityEngine 里也有一个 AudioType，和项目自己的同名 —— 不钉住的话 CaseSpec 里那个字段会报 CS0104。
using AudioType = Project.Core.Runtime.Framework.AudioType;

namespace Project.Gameplay.Scripts.Case
{
    /// <summary>一次观测的读数类型（决定要用哪把工具、以及异常长什么样）。</summary>
    public enum CaseReadingKind
    {
        None = 0,
        Temperature = 1,
        Emf = 2,
        Uv = 3,
        Audio = 4,

        /// <summary>工具包：拆开夹层、撬开柜门这类"物理观测"。</summary>
        Physical = 5,
    }

    /// <summary>一件家具在本局里的"案情"：用哪把工具读、读到什么、这次是不是真的有问题。</summary>
    [Serializable]
    public struct CaseSpec
    {
        public string InteractableId;
        public string DisplayName;
        public CaseReadingKind Kind;
        public ToolType Tool;
        public float Temperature;
        public int Emf;
        public bool Uv;

        /// <summary>录音读数的内容（录音笔用：异常 = 嘶吼，正常 = 平稳呼吸）。</summary>
        public AudioType Audio;

        /// <summary>这条读数是不是异常 ✓（= 档位不是「正常」✓ —— 见 <see cref="ReadingStrength"/> ✓）。</summary>
        public bool IsAnomaly;

        /// <summary>
        /// 这件家具**离主场多近** ✓（0~1 ✓）—— 每条读数落"强 / 弱 / 正常"哪一档由它算出来 ✓。
        /// 诱饵的强度是 **0** ✓（离得近、读数却比远处还干净 ✓ —— 故意的 ✓）。
        /// </summary>
        public float AnomalyStrength;

        /// <summary>玩家是否已经读过它 ✓（**证据按"一处产地算一条"** ✓ —— 闸门就在这儿 ✓）。</summary>
        public bool Read;

        /// <summary>这件家具一共读出过几条读数 ✓（五把工具各一条 ✓ —— 判定权重迟早要拿它算 ✓）。</summary>
        public int ReadingsTaken;

        /// <summary>其中几条是异常的 ✓（和上一条一起决定"这件到底有多可疑"✓）。</summary>
        public int AnomalousReadings;

        /// <summary>读数文案（由 CaseDirector 生成，规则直接拿去显示）。</summary>
        public string SuccessText;
    }

    /// <summary>
    /// 本局案情（恐鬼症式）：开局按种子决定"目标是伪人还是正常人"，再把读数随机分到房间里的家具上；
    /// 玩家用工具去读，读数里凑够互相印证的异常后自己下结论，由这里结算胜负。
    ///
    /// 为什么要有它：关卡内容不再写死在场景里（通用关卡器），
    /// 场景只负责"房间长什么样"，"这一局是什么案子"由这里在运行时摇。
    ///
    /// 分工：
    /// - <see cref="ResolveSeed"/>：决定本局种子，Bootstrapper 拿它去 Initialize BranchManager（播种只在一处）。
    /// - <see cref="BeginCase"/>：摇案情 + 把读数灌进每件家具的 <see cref="SampleInteractableRule"/>。
    /// - <see cref="NotifyRead"/>：规则读完一条观测后报回来，这里统计异常数并刷新证据读数。
    /// - <see cref="SubmitVerdict"/>：玩家下结论，和本局身份比对 → Victory / GameOver。
    /// </summary>
    public sealed class CaseDirector : MonoBehaviour
    {
        [Header("随机种子")]
        [Tooltip("勾上 = 每局都用 fixedSeed（方便复现/调试）；关掉 = 每局新种子。")]
        [SerializeField] private bool useFixedSeed = true;
        [SerializeField] private int fixedSeed = 2050;

        [Header("身份")]
        [Tooltip("本局目标是伪人的概率（只在没勾 forceIdentity 时用）。")]
        [SerializeField, Range(0f, 1f)] private float fakeHumanChance = 0.5f;
        [Tooltip("勾上 = 身份不看种子，直接用下面这个开关（调「必伪人 / 必正常人」用）。")]
        [SerializeField] private bool forceIdentity;
        [Tooltip("forceIdentity 勾上时：true = 本局必定是伪人。")]
        [SerializeField] private bool forcedFakeHuman = true;

        [Header("读数种类表")]
        [Tooltip("读数种类表 ✓（`Tools/Project/Case/Create Reading Kind Table` 生成 ✓）。\n"
                 + "**留空也能跑** ✓ —— 那时用代码里的兜底表 ✓；想加新读数就在表里加一行 ✓。")]
        [SerializeField] private ReadingKindTable readingKindTable;

        private ReadingKindTable fallbackTable;

        [Header("异常强度（主场衰减 + 诱饵 ✓ —— 见 Docs/ContainmentRules.md §5.3-1 ✓）")]
        [Tooltip("衰减半径（像素 ✓）。**留 0 = 按房间自己算** ✓（取最远那对家具距离的一半 ✓）—— 换场景不用重填 ✓。")]
        [SerializeField] private float anomalyDecayRadius;

        [Tooltip("强度抖动 ±这个数 ✓。0 = 只有距离说了算 ✓（同心圆 ✗，玩家拿尺子量距离就能反推 ✗）。")]
        [SerializeField, Range(0f, 0.5f)] private float anomalyJitter = 0.18f;

        [Tooltip("诱饵件数上限 ✓：从离主场最近的那几件里挑 ✓，它的读数反而比远处还干净 ✓。")]
        [SerializeField] private int maxDecoys = 2;

        [Tooltip("这一局出现诱饵的概率 ✓ —— **默认 10%** ✓：诱饵要当**意外** ✓，"
                 + "每局都来就变成一条可预期的规律了 ✗（玩家两局就会学会\"近处不算数\"✗）。")]
        [SerializeField, Range(0f, 1f)] private float decoyChance = 0.1f;

        [Tooltip("**主场可以有几个** ✓：1 = 单源 ✓、2 = 双源 ✓（两个源各自衰减 ✓，中间会互相叠加 ✓）。")]
        [SerializeField, Range(1, 4)] private int maxOrigins = 2;

        [Tooltip("**调试用** ✓：填家具关键词（例如「沙发」「木桌」✓）就**强制**这几件当主场 ✓，留空 = 按种子抽 ✓。\n"
                 + "想复现某个特定布局（比如故意让两个源离得很远 ✓）就填它 ✓。※ 上线前必须清空 ✗。")]
        [SerializeField] private string[] debugForceOrigins = new string[0];

        /// <summary>
        /// 用的那张读数种类表 ✓：接了资产就用资产 ✓，没接就用兜底表 ✓
        ///（谁忘了连资产都不会把游戏弄坏 ✗ ✓）。
        /// </summary>
        private ReadingKindTable Table
        {
            get
            {
                if (readingKindTable != null)
                {
                    return readingKindTable;
                }

                return fallbackTable != null
                    ? fallbackTable
                    : fallbackTable = ReadingKindTable.CreateDefaultInstance();
            }
        }

        [Header("装备")]
        [Tooltip("可带的工具池（5 件）。每局从这里随机少带一件 —— 装备位有几格就带几件；留空 = 不换装备。")]
        [SerializeField] private ToolItem[] loadoutPool = new ToolItem[0];

        [Header("收容物（见 Docs/ContainmentRules.md §1、§3.3）")]
        [Tooltip("家具名关键词：家具 id（obj_3b水渍）里含它的才有资格当产地 ✓。和下面两条按位置一一对应 ✓。")]
        [SerializeField] private string[] containmentHostKeywords = { "水渍", "沙发", "木桌", "茶几", "吊灯", "书籍" };
        [Tooltip("**异常**家具产出的收容物 id ✓（伪人物品 ✓，丢弃要扣 SAN ✓）。")]
        [SerializeField] private string[] containmentClueIdsAnomaly =
        {
            "clue_water_stain", "clue_sofa_hair", "clue_drawer_record",
            "clue_group_photo", "clue_ceiling_print", "clue_glass_vial",
        };
        [Tooltip("**正常**家具产出的收容物 id ✓（同样是收容物 ✓ 也能收容 ✓，只是不扣 SAN ✓）。")]
        [SerializeField] private string[] containmentClueIdsNormal =
        {
            "clue_water_stain_normal", "clue_sofa_hair_normal", "clue_drawer_record_normal",
            "clue_group_photo_normal", "clue_ceiling_print_normal", "clue_glass_vial_normal",
        };
        [Tooltip("本局收容物件数的下限 —— 从种子摇 ✓。设成 0 就允许「这一局一件收容物都没有」✓。")]
        [SerializeField] private int minContainmentClues = 1;
        [Tooltip("上限 ✓。收容箱一共 3 格 ✓，别设得比它大太多 ✗。")]
        [SerializeField] private int maxContainmentClues = 3;
        [Tooltip("只在伪人局产出收容物 ✗ —— **默认关掉** ✓：正常人局也有正常收容物 ✓，只是不扣 SAN ✓。")]
        [SerializeField] private bool containOnlyInFakeHumanCase;

        /// <summary>
        /// 本局可用的**整池**工具 ✓（= 背包里那几件 ✓）—— bootstrapper 拿它填 `InventoryManager.SetBackpackItems` ✓。
        /// 注意和 <see cref="EquippedTools"/> 的区别 ✗：那个是**案情决定的、装进工具包的那套** ✓（4 件 ✓），
        /// 这个是**背包里的全部** ✓（5 件 ✓），两者的差就是玩家能自己换的那一格 ✓。
        /// </summary>
        public IReadOnlyList<ToolItem> LoadoutPool => loadoutPool;

        /// <summary>本局**收容物产地**的一行 ✓：哪件家具 ✓、产出哪件收容物 ✓。</summary>
        public readonly struct ClueSite
        {
            public readonly string Host;
            public readonly string ClueId;
            public readonly string ClueName;

            public ClueSite(string host, string clueId, string clueName)
            {
                Host = host;
                ClueId = clueId;
                ClueName = clueName;
            }
        }

        private readonly List<ClueSite> clueSites = new();

        /// <summary>
        /// 本局**真正挂上**的收容物产地 ✓（挂载失败的不算 ✓）。
        ///
        /// 为什么要它 ✗→✓：产地是按种子抽的 ✓（池子里 6 处只挑 1~3 处 ✓），
        /// 玩家（和测试时的我们自己 ✓）光看"产地 1 处"根本不知道该去翻哪件家具 ✗ ——
        /// 「收容」页拿这份清单直接写"去哪儿拿什么、收没收" ✓。
        /// </summary>
        public IReadOnlyList<ClueSite> ClueSites => clueSites;

        [Tooltip("**调试用** ✓：打开后「收容」页会把本局产地（哪件家具有什么）直接列出来 ✓。"
                 + "默认**必须关着** ✗ —— 那等于把「该翻哪儿」告诉玩家 ✓，调查就没得玩了 ✗。")]
        [SerializeField] private bool debugShowClueSites;

        /// <summary>「收容」页是否列出产地清单 ✓（默认关 ✓，见上面那条 Tooltip 的理由 ✗）。</summary>
        public bool DebugShowClueSites => debugShowClueSites;

        [Header("线索分布")]
        [Tooltip("本局读数异常的家具件数下限（会被房间里的家具总数夹住）。")]
        [SerializeField] private int minAnomalyTargets = 2;
        [Tooltip("上限。")]
        [SerializeField] private int maxAnomalyTargets = 4;
        [Tooltip("要「确定异常」需要互相印证几条异常读数；也是 HUD 上证据读数的目标值。")]
        [SerializeField] private int corroborationNeeded = 2;
        [Tooltip("每件家具需要先点开检视、工具才生效的概率（0 = 都不需要）。")]
        [SerializeField, Range(0f, 1f)] private float inspectionRequiredChance = 0.25f;

        [Header("读数区间（异常）")]
        [SerializeField] private Vector2 anomalyTemperature = new Vector2(2f, 9f);
        [SerializeField] private Vector2 anomalyEmf = new Vector2(4f, 5f);

        [Header("读数区间（正常）")]
        [SerializeField] private Vector2 normalTemperature = new Vector2(20f, 26f);
        [SerializeField] private Vector2 normalEmf = new Vector2(0f, 2f);

        [Header("调试")]
        [SerializeField] private bool logCase = true;

        private readonly Dictionary<string, CaseSpec> specs = new();
        private readonly List<string> readOrder = new();
        private readonly List<ToolItem> equippedTools = new();
        private int seed;
        private bool isFakeHuman;
        private int foundAnomalies;
        private bool submitted;

        /// <summary>本局种子。</summary>
        public int Seed => seed;

        /// <summary>本局目标是不是伪人（**玩家不该直接看到**，只给结算和日志用）。</summary>
        public bool IsFakeHuman => isFakeHuman;

        /// <summary>案情是否已经摇好。</summary>
        public bool HasCase => specs.Count > 0;

        /// <summary>已经读到的异常条数。</summary>
        public int FoundAnomalies => foundAnomalies;

        /// <summary>已经读过读数的家具件数。</summary>
        public int ReadingCount => readOrder.Count;

        /// <summary>「确定异常」需要的异常条数。</summary>
        public int CorroborationNeeded => corroborationNeeded;

        /// <summary>异常条数够不够互相印证。</summary>
        public bool IsEnoughToConclude => foundAnomalies >= corroborationNeeded;

        /// <summary>结论是否已经交过（交完就不能再交）。</summary>
        public bool HasSubmitted => submitted;

        /// <summary>上一次提交的结算文案（给结算界面/提示行用）。</summary>
        public string LastResultText { get; private set; } = string.Empty;

        /// <summary>上一次提交判对了没有（没提交过时为 false）。</summary>
        public bool LastVerdictCorrect { get; private set; }

        /// <summary>本局实际带进来的工具（空 = 没配工具池，沿用场景里的固定装备）。</summary>
        public IReadOnlyList<ToolItem> EquippedTools => equippedTools;

        /// <summary>
        /// 已经读到的观测（按读取顺序）—— 给 SmallApp 里的笔记视图显示用。
        /// 视图只负责显示，判断仍然全在本类里（所以这里只给只读快照，不给内部字典）。
        /// </summary>
        public IReadOnlyList<CaseSpec> ReadSpecs
        {
            get
            {
                var list = new List<CaseSpec>(readOrder.Count);
                foreach (var id in readOrder)
                {
                    if (specs.TryGetValue(id, out var spec))
                    {
                        list.Add(spec);
                    }
                }

                return list;
            }
        }

        /// <summary>
        /// 本局**所有**家具的案情（含还没读过的）—— 给"线索清单"页用：
        /// 未读的显示成待查，读过的带读数（`CaseSpec.Read` 就是标记）。
        /// </summary>
        public IReadOnlyList<CaseSpec> AllSpecs
        {
            get
            {
                var list = new List<CaseSpec>(specs.Count);
                foreach (var pair in specs)
                {
                    list.Add(pair.Value);
                }

                // 按 id 排序，保证列表顺序稳定（字典的枚举顺序不保证 ✗）。
                list.Sort((a, b) => string.CompareOrdinal(a.InteractableId, b.InteractableId));
                return list;
            }
        }

        /// <summary>
        /// 笔记那一页的文字（进度 + 观测列表）。让 UI 不用自己拼格式 ——
        /// 现在只在 nothink 里显示的那行"证据读数"也搬到这里。
        /// </summary>
        public string BuildJournalText()
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine($"案号 PX-2050-734　·　本局种子 {seed}");
            text.AppendLine($"异常读数 {foundAnomalies}/{corroborationNeeded}"
                            + (IsEnoughToConclude ? "（已经够互相印证）" : "（还不够互相印证）"));
            text.AppendLine();

            if (readOrder.Count == 0)
            {
                text.AppendLine("还没有读到任何读数。用底部工具槽里的工具去查房间里的东西。");
                AppendResult(text);
                return text.ToString();
            }

            foreach (var spec in ReadSpecs)
            {
                // 交互物 id 是建造工具按文件名生成的（obj_3b木桌），这里去掉前缀，玩家只看家具名。
                var name = (spec.DisplayName ?? string.Empty)
                    .Replace("obj_3b", string.Empty)
                    .Replace("obj_3a", string.Empty);

                text.AppendLine($"· {name}：{spec.SuccessText}");
            }

            AppendResult(text);
            return text.ToString();
        }

        /// <summary>
        /// 提交结论之后追加的「结论」段 ✓。
        ///
        /// 为什么写进笔记正文、而不是另开一屏结算界面：玩家本来就是**坐在笔记这一页上**下的结论 ✓，
        /// 结果长在同一页上最自然 ✓。原来那块盖住整个窗口的结算板（CaseResult）已经不要了 ✗；
        /// 它那两个按钮（再调查一次 / 结束调查）现在由笔记页自己的两个格子接手 ✓。
        /// </summary>
        private void AppendResult(System.Text.StringBuilder text)
        {
            if (!submitted)
            {
                return;
            }

            text.AppendLine();
            text.AppendLine("── 结论 ──");
            text.AppendLine(LastVerdictCorrect ? "判断正确 ✓" : "判断错误 ✗");

            if (!string.IsNullOrEmpty(LastResultText))
            {
                text.AppendLine(LastResultText);
            }

            text.AppendLine($"真相：目标其实是{(isFakeHuman ? "伪人" : "普通人")}。");
            text.AppendLine($"读数：异常 {foundAnomalies} 条（判定需要 {corroborationNeeded} 条），"
                            + $"一共读过 {readOrder.Count} 件家具。");
            text.AppendLine($"本局种子 {seed}（同一颗种子会摇出同一份案情）");

            // 只有"读数不够却判对"和"读数够了却判错"这两种情况值得多说一句 ✓，其它情况不用画蛇添足 ✗。
            if (LastVerdictCorrect && !IsEnoughToConclude)
            {
                text.AppendLine("读数还不够互相印证 —— 这一把是赌对的 ✓");
            }
            else if (!LastVerdictCorrect && IsEnoughToConclude)
            {
                text.AppendLine("读数其实已经够互相印证了，但结论下反了 ✗");
            }

            text.AppendLine("要接着查就按「再调查一次」，看完了按「结束调查」。");
        }

        private void Awake()
        {
            // 注册成服务，UI 层（结论选项）直接 TryGet 就能拿到，不用在场景里连引用。
            Services.Register<CaseDirector>(this);
        }

        /// <summary>
        /// 决定本局的种子。Bootstrapper 拿它去 Initialize BranchManager ——
        /// 全项目只有那一处播种，案情用同一颗种子自己算，互不依赖调用顺序。
        /// </summary>
        public int ResolveSeed()
        {
            // 序幕来的交接单优先：这一关的种子由序幕定（"固定关卡"就是靠这个），关卡不再自己摇。
            if (CaseHandoff.HasRequest)
            {
                seed = CaseHandoff.Seed;
                return seed;
            }

            seed = useFixedSeed
                ? fixedSeed
                : unchecked((int)(DateTime.Now.Ticks & 0x7FFFFFFF));

            return seed;
        }

        /// <summary>
        /// 摇出本局案情并灌进每件家具。Bootstrapper 在库存 / 装备 / flag 都就绪之后调用。
        /// 传入的种子必须和 <see cref="ResolveSeed"/> 返回的是同一颗。
        /// </summary>
        public void BeginCase(int seedValue)
        {
            seed = seedValue;
            specs.Clear();
            readOrder.Clear();
            clueSites.Clear();
            foundAnomalies = 0;
            submitted = false;

            // 随机全部走"分组的桶"（CaseRandomBuckets）：**同组共用一个流、组间独立**。
            // 案情总流管：异常件数、挑哪几件是异常的、整体布局。
            var rng = CaseRandomBuckets.Bucket(CaseGroups.Case);

            // 身份走自己的组：这样"身份是摇的还是指定的"不会影响别的组
            // （一条流按调用顺序消耗时，改上面任何一处都会把下面全部挪位）。
            isFakeHuman = forceIdentity
                ? forcedFakeHuman
                : CaseRandomBuckets.Bucket(CaseGroups.Identity).Chance(fakeHumanChance);

            // 本局带哪几件：序幕如果有交接单（它已经在自己的 UI 上显示了那几件），就**照它来**；
            // 否则自己从工具池里随机少带一件（像"带什么进场"）。
            if (CaseHandoff.HasRequest && CaseHandoff.Loadout.Length > 0)
            {
                equippedTools.Clear();
                foreach (var tool in CaseHandoff.Loadout)
                {
                    if (tool != null)
                    {
                        equippedTools.Add(tool);
                    }
                }

                if (logCase)
                {
                    Debug.Log($"[Case] 本局装备来自序幕交接单：{equippedTools.Count} 件。");
                }
            }
            else
            {
                // 装备自己一个组：在案情里加减随机不会动到"这一局带什么"。
                BuildLoadout(CaseRandomBuckets.Bucket(CaseGroups.Loadout));
            }

            var availableKinds = BuildAvailableKinds(equippedTools);

            // 位置按贴图 alpha 重心算 ✓ —— 家具在运行中可能被挪过（水渍那种 ✓），
            // 所以每局开头清一次缓存 ✓，别把上一局的位置带到这一局 ✗。
            CenterCache.Clear();

            var targets = FindTargets();
            if (targets.Count == 0)
            {
                Debug.LogWarning("[Case] 场景里没有找到任何交互物，案情没有生成。");
                return;
            }

            // **异常有源头** ✓：不再是"随便挑几件算异常" ✗ —— 按种子挑 **1 件主场** ✓，
            // 再按**距离衰减**给每件家具算强度 ✓（越靠近源头越强 ✓），最后插 1~2 件**诱饵** ✓
            //（离源头近、读数反而比远处更干净 ✓ —— 故意的混淆视听 ✓，见 Docs/ContainmentRules.md §5.3-1 ✓）。
            // 正常人局没有源头 ✓ → 全部强度 0 ✓（玩家该读到"哪儿都没问题"✓）。
            var strengths = new float[targets.Count];
            var decoySet = new HashSet<int>();
            var origins = new List<int>();

            if (isFakeHuman)
            {
                origins = PickOrigins(targets, rng);
                ApplyAnomalyStrengths(targets, origins, strengths, rng);
                PickDecoys(targets, origins, strengths, decoySet, rng);
            }

            // "异常家具" = 强度够高那几件 ✓ —— 下游的**产地优先 / 证据 / 判定全部照旧用它** ✓，
            // 只是它的含义从"随机挑中"变成了"**离源头够近**"✓（诱饵强度是 0 ✓ 自然不算 ✓）。
            var anomalySet = new HashSet<int>();
            for (var i = 0; i < strengths.Length; i++)
            {
                if (strengths[i] >= AnomalyThreshold)
                {
                    anomalySet.Add(i);
                }
            }

            // 收容物产地：**由种子挑** ✓，不是写死的映射 ✗ —— 件数摇 ✓、产地在池子里挑 ✓、异常家具优先 ✓。
            // 于是：同一颗种子永远同一批产地 ✓（这就是你说的"部分交互点固定产出"✓），换种子才会换位置 ✓。
            var clueHosts = PickContainmentHosts(targets, anomalySet, rng, isFakeHuman);

            for (var i = 0; i < targets.Count; i++)
            {
                var rule = targets[i].Rule;
                var interactable = targets[i].Interactable;
                var id = interactable.InteractableId;

                // 每件家具一条自己的流：一件家具里多摇一次，不会影响别的家具（相邻家具最容易互相挪位）。
                var furnitureRng = CaseRandomBuckets.Bucket(CaseGroups.Furniture(id));

                var kind = PickKind(furnitureRng, availableKinds);
                var isAnomaly = anomalySet.Contains(i);
                var spec = BuildSpec(furnitureRng, interactable, kind, isAnomaly);

                // 这件家具离**主场**多近 ✓ —— 每条读数落"强 / 弱 / 正常"哪一档，就由它算 ✓。
                spec.AnomalyStrength = strengths[i];

                specs[id] = spec;
                rule.ConfigureFromCase(spec, inspectionRequiredChance > 0f && furnitureRng.NextDouble() < inspectionRequiredChance);

                // **一件家具一整套读数** ✓：种类 ✓、工具 ✓、三档数值 ✓、三档文案 ✓ **全部来自读数种类表** ✓，
                // 而"落哪一档"由**强度 × 这条读数的敏感度**决定 ✓ —— 于是同一件上天然有强有弱 ✓
                //（不再是"每条各掷 30%"那种和空间无关的随机 ✗）。
                foreach (var reading in BuildReadings(spec, furnitureRng))
                {
                    rule.AddReading(reading);
                }

                // 这件家具是不是本局的收容物产地 ✓ —— 由上面按种子挑好的表决定 ✓。
                // 产哪一件看**这处异常不异常** ✓：异常家具给伪人物品 ✓、正常家具给正常收容物 ✓
                //（两件都是收容物 ✓ 都能收容 ✓，只有异常的丢弃才扣 SAN ✓ —— 见 Docs/ContainmentRules.md §1）。
                if (clueHosts.TryGetValue(i, out var pair))
                {
                    AttachContainmentClue(rule, spec, spec.IsAnomaly ? pair.Anomaly : pair.Normal);
                }
            }

            // 证据读数的目标值就是"要凑够几条异常"：EvidenceManager 顺带负责 HUD 那行显示和 flag。
            if (Services.TryGet<EvidenceManager>(out var evidenceManager))
            {
                evidenceManager.Initialize(corroborationNeeded);
            }

            // 交接单只生效一次：用完就清，免得下一局又套用上一局的种子/装备。
            CaseHandoff.Clear();

            if (logCase)
            {
                var loadoutText = equippedTools.Count > 0
                    ? string.Join(" / ", equippedTools.ConvertAll(t => t != null ? t.DisplayName : "?"))
                    : "场景固定装备";

                // **主场 / 诱饵必须打出来** ✓ —— 这套模型光看"异常 3 件"根本验证不了 ✗：
                // 有了这两行，一局打完就能对照"谁在源头 ✓、谁是诱饵 ✓、读数档位对不对"✓。
                var originText = origins.Count > 0
                    ? string.Join(" / ", origins.ConvertAll(index => targets[index].Interactable.InteractableId))
                    : "（无 ✓ 正常人局）";

                var decoyNames = new List<string>();
                foreach (var index in decoySet)
                {
                    decoyNames.Add(targets[index].Interactable.InteractableId);
                }

                var decoyText = decoyNames.Count > 0 ? string.Join(" / ", decoyNames) : "无";

                Debug.Log($"[Case] 本局案情已生成：种子 {seed}，身份 {(isFakeHuman ? "伪人" : "正常人")}，"
                          + $"家具 {targets.Count} 件，其中异常（强度 ≥ {AnomalyThreshold:0.00}）{anomalySet.Count} 件，"
                          + $"需要 {corroborationNeeded} 条互相印证；带进场：{loadoutText}。");
                // **用的是哪张表必须打出来** ✓ —— 否则"资产接了没生效"✗ 只能靠翻代码反推 ✓
                //（这一次就是这么被问出来的 ✗）：一眼就能分清"在跑资产 ✓"还是"资产没接 ✗、跑兜底 ✓"。
                Debug.Log($"[Case] 读数种类表：{(readingKindTable != null ? $"{readingKindTable.name}（资产 ✓）" : "代码兜底 ✓ —— 资产没接 ✗")}；"
                          + $"主场：{originText}；"
                          + $"诱饵（离得近、读数反而更干净 ✓）：{decoyText}；"
                          + $"衰减半径 {(int)ResolveDecayRadius(targets)} 像素（0 = 按房间自动 ✓）");

                // **每件家具的强度也打出来** ✓ —— "这一局的异常到底在哪"就不该靠猜 ✗：
                // 强度最高的是源头 ✓、标 ☆ 的是诱饵（强度 0 ✓）、标 ● 的是够门槛的异常家具 ✓。
                for (var i = 0; i < targets.Count; i++)
                {
                    var marker = origins.Contains(i)
                        ? " ★主场"
                        : decoySet.Contains(i)
                            ? " ☆诱饵"
                            : anomalySet.Contains(i) ? " ●异常" : string.Empty;

                    var center = WorldCenter(targets[i].Interactable);

                    Debug.Log($"[Case] 强度：{targets[i].Interactable.InteractableId} = {strengths[i]:0.00}{marker}"
                              + $"（中心 {center.x:0}, {center.y:0}）");
                }
            }
        }

        /// <summary>
        /// 按种子挑出本局的收容物产地 ✓（表在 Inspector 上，见 Docs/ContainmentRules.md §1）。
        ///
        /// 规则：
        ///  - 只在伪人局 ✓（正常人局没有「伪人物品」可收 ✓）；
        ///  - **件数**从案情总流摇 ✓（`Case` 桶 → 跟种子走 ✓），范围由 min/max 控制 ✓；
        ///  - 产地只在「关键词 ↔ 收容物」池子里挑 ✓ —— 池子是写死的 ✓，因为每件收容物的美术本来
        ///    就和某件家具绑死 ✓（荧光水渍只能长在水渍上 ✗ 不可能长在吊灯上 ✓）；
        ///  - **异常家具优先** ✓：先挑本局判成异常的那些 ✓，不够再按洗牌顺序补 ✓。
        ///    优先是刻意的 ✓：读数指哪儿，那里就该收东西 ✓ —— 玩家的"查 → 收"才是一条线 ✓。
        ///
        /// 所以收容物**不是**固定长在三件家具上 ✗：换种子就换位置 ✓，也可能一件都不出 ✓（min 设 0 时 ✓）。
        /// </summary>
        private Dictionary<int, (string Anomaly, string Normal)> PickContainmentHosts(
            IReadOnlyList<Target> targets, HashSet<int> anomalies, CaseRandom rng, bool fakeHuman)
        {
            var result = new Dictionary<int, (string Anomaly, string Normal)>();
            if (containOnlyInFakeHumanCase && !fakeHuman)
            {
                return result;
            }

            if (containmentHostKeywords == null
                || containmentClueIdsAnomaly == null
                || containmentClueIdsNormal == null)
            {
                return result;
            }

            var pairCount = Mathf.Min(
                containmentHostKeywords.Length,
                Mathf.Min(containmentClueIdsAnomaly.Length, containmentClueIdsNormal.Length));

            // 池子：谁的 id 里含关键词，谁就可能是收容物产地 ✓（一件家具最多挂一件 ✓）。
            // 每一处**成对**存两个 id ✓：异常版给伪人物品 ✓、正常版给正常收容物 ✓。
            var pool = new List<(int Index, string Anomaly, string Normal)>();
            for (var i = 0; i < targets.Count; i++)
            {
                var interactable = targets[i].Interactable;
                var id = interactable != null ? interactable.InteractableId : string.Empty;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                for (var k = 0; k < pairCount; k++)
                {
                    var keyword = containmentHostKeywords[k];
                    if (!string.IsNullOrEmpty(keyword) && id.IndexOf(keyword, System.StringComparison.Ordinal) >= 0)
                    {
                        pool.Add((i, containmentClueIdsAnomaly[k], containmentClueIdsNormal[k]));
                        break;
                    }
                }
            }

            if (pool.Count == 0)
            {
                Debug.LogWarning("[Case] 收容物池子是空的 ✗ —— 检查 containmentHostKeywords 有没有对得上家具 id ✓。");
                return result;
            }

            var upper = Mathf.Clamp(maxContainmentClues, 0, pool.Count);
            var lower = Mathf.Clamp(minContainmentClues, 0, upper);
            var want = rng.Next(lower, upper + 1);
            if (want <= 0)
            {
                return result;
            }

            var order = BuildShuffledOrder(pool.Count, rng);
            var picked = new List<int>();

            // 第一轮：异常家具优先 ✓
            foreach (var oi in order)
            {
                if (picked.Count >= want)
                {
                    break;
                }

                if (anomalies.Contains(pool[oi].Index))
                {
                    picked.Add(oi);
                }
            }

            // 第二轮：还不够就按洗牌顺序补 ✓ —— 不然"异常正好没落在池子里"就会一件都不出 ✗。
            foreach (var oi in order)
            {
                if (picked.Count >= want)
                {
                    break;
                }

                if (!picked.Contains(oi))
                {
                    picked.Add(oi);
                }
            }

            foreach (var oi in picked)
            {
                result[pool[oi].Index] = (pool[oi].Anomaly, pool[oi].Normal);
            }

            if (logCase)
            {
                // 直接把"抽中了哪几件"写出来 ✓ —— 不然只有"1 处"这个数字 ✓，
                // 玩家/你自己还得去猜该翻哪件家具 ✗（我们已经为这个绕了两轮 ✓）。
                var hostNames = new List<string>();
                foreach (var index in result.Keys)
                {
                    var interactable = targets[index].Interactable;
                    hostNames.Add(interactable != null ? interactable.InteractableId : index.ToString());
                }

                Debug.Log($"[Case] 本局收容物产地 {result.Count} 处（种子 {seed}，异常 {anomalies.Count} 件，"
                          + $"池子 {pool.Count} 处）：{string.Join(" / ", hostNames)}");
            }

            return result;
        }

        /// <summary>把某件家具变成收容物产地 ✓（id 由 <see cref="PickContainmentHosts"/> 按种子 + 异常与否挑好 ✓）。</summary>
        private void AttachContainmentClue(SampleInteractableRule rule, CaseSpec spec, string clueId)
        {
            if (rule == null)
            {
                // 以前这里**静默**返回 ✗ —— 于是"产地 1 处、却没有任何一行产地"✗，
                // 找起来要多绕两轮 ✓。失败就说出来 ✓。
                Debug.LogWarning($"[Case] 收容物没挂上：{spec.DisplayName} 上没有 SampleInteractableRule ✗（{clueId}）");
                return;
            }

            var clue = FindClueById(clueId);
            if (clue == null)
            {
                // 找不到就说清楚 ✗：多半是 ItemId 拼错了，或者这件收容物没进 bootstrapper 的 knownItems ✓。
                Debug.LogWarning($"[Case] 收容物产地「{spec.DisplayName}」找不到收容物 id「{clueId}」"
                                 + "（检查 ClueItem 的 ItemId，以及它在不在 knownItems 里 ✓）。");
                return;
            }

            rule.AttachClue(clue, clue.EvidenceId, true);

            // 记进"本局产地表" ✓ —— 「收容」页靠它列出这一局能收什么 ✓（挂载失败的不记 ✓）。
            clueSites.Add(new ClueSite(spec.DisplayName, clue.ItemId, clue.DisplayName));

            if (logCase)
            {
                Debug.Log($"[Case] 收容物产地：{spec.DisplayName} → {clue.DisplayName}（{clue.ItemId}，进收容箱 ✓）");
            }
        }

        /// <summary>强度够这个数就算"异常家具" ✓（§2 里"可以直接下结论"那道门槛 ✓）。</summary>
        private const float AnomalyThreshold = 0.5f;

        /// <summary>读数落"强"档的门槛 ✓（够硬 = 光靠它也能下结论 ✓）。</summary>
        private const float StrongReadingThreshold = 0.66f;

        /// <summary>读数落"弱"档的门槛 ✓（有点不对劲 ✓，需要旁证 ✓）。</summary>
        private const float WeakReadingThreshold = 0.33f;

        /// <summary>
        /// 一件家具在本局的**一整套读数** ✓ —— **表里有几种读数就有几条** ✓
        ///（现在 5 条 ✓ = 五把工具各一条 ✓，就是"我能不能用 5 件道具测同一件家具"✓：能 ✓）。
        ///
        /// 种类 ✓、工具 ✓、三档数值 ✓、三档文案 ✓ **全部来自 <see cref="ReadingKindTable"/>** ✓ ——
        /// 以前是写死的 5 种 + 6 个 switch ✗（加一种读数要改 8 处 ✗），现在表里加一行就行 ✓。
        ///
        /// 落档 = **这件家具的异常强度 × 这条读数的敏感度** ✓（<see cref="ReadingKindDefinition.Sensitivity"/> ✓）：
        /// 灵敏的（探测器 ✓）远处也能读到一点 ✓，迟钝的（工具包 ✓）非贴到源头拆不出东西 ✓ ——
        /// "同一件家具上有的正常 ✓、有的异常 ✓"是**空间算出来**的 ✓，不是掷骰子掷出来的 ✓。
        /// </summary>
        /// 每档下面挂了几条候选 ✓，就能摇出几种结果 ✓（见 <see cref="ReadingVariant"/> ✓）——
        /// **数值和措辞每局都不一样** ✓；固定下来的只有"档位"（= 语义 ✓）。
        private SampleInteractableRule.ToolReading[] BuildReadings(CaseSpec spec, CaseRandom rng)
        {
            var definitions = Table.Kinds;
            var result = new List<SampleInteractableRule.ToolReading>(definitions.Count);

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition.Kind == CaseReadingKind.None)
                {
                    continue;
                }

                // 敏感度是"这条读数多容易读到东西" ✓ —— 不接线（0）会让所有读数永远正常 ✗，所以兜个下限 ✓。
                var reach = spec.AnomalyStrength * Mathf.Max(0.01f, definition.Sensitivity);

                result.Add(BuildReading(spec, definition, StrengthOf(reach), rng));
            }

            return result.ToArray();
        }

        /// <summary>强度 → 档位 ✓（两道门槛照 §2 两张表 ✓）。</summary>
        private static ReadingStrength StrengthOf(float strength)
        {
            if (strength >= StrongReadingThreshold)
            {
                return ReadingStrength.Strong;
            }

            return strength >= WeakReadingThreshold ? ReadingStrength.Weak : ReadingStrength.Normal;
        }

        /// <summary>
        /// 拼一条读数 ✓：工具 ✓ + 观测值 ✓ + 文案 ✓ + **它自己落哪一档** ✓ —— 内容全部查表 ✓。
        ///
        /// **随机就发生在这一步** ✓：这一档下面有几种候选 ✓ 就能摇出几种结果 ✓ ——
        /// 先**挑一条候选** ✓ → 在它自己的 `[Min, Max]` 里**摇一个数** ✓ → 把数写进那条文案的 `{0}` ✓。
        /// 于是"温度枪：-3.2℃"和"温度枪：-7.8℃，探头刚放上去读数就往下掉。"都可能出现 ✓，
        /// 而**档位语义不动** ✓（强档就是明显偏低 ✓、弱档就是需要旁证 ✓）。
        ///
        /// 文案优先级 ✓：候选里摇出来的那句 ✓ → **家具覆盖表**里这件家具的专属文案 ✓（也支持 `{0}` ✓）。
        /// </summary>
        private SampleInteractableRule.ToolReading BuildReading(
            CaseSpec spec, ReadingKindDefinition definition, ReadingStrength strength, CaseRandom rng)
        {
            var kind = definition.Kind;

            var variant = definition.Pick(strength, rng);
            var value = variant.Roll(rng);
            var valueText = definition.FormatNumber(value);

            var reading = new SampleInteractableRule.ToolReading
            {
                Tool = definition.Tool,
                ToolDisplayName = definition.DisplayName,
                Kind = kind,
                IsAnomaly = strength != ReadingStrength.Normal,

                // 两个数值字段都填同一个数 ✓ —— 只有 Kind 对应的那一个会被读走 ✓
                //（观测位由 Kind 决定 ✓，见 SampleInteractableRule.RegisterObservation ✓）。
                Temperature = value,
                Emf = Mathf.RoundToInt(value),
                Uv = variant.Flag,
                Audio = variant.Audio,
                Text = variant.BuildText(valueText),
            };

            if (Table.TryGetOverrideText(spec.InteractableId, kind, strength, valueText, out var overrideText))
            {
                reading.Text = overrideText;
            }

            return reading;
        }

        /// <summary>
        private static readonly Dictionary<int, Vector2> CenterCache = new Dictionary<int, Vector2>();

        /// <summary>
        /// 这件家具在房间里的**中心** ✓（画布像素坐标 ✓）。
        ///
        /// ⚠ **这个函数栽过三次** ✗，记清楚免得第四次：
        ///  ① `transform.position` ✗；② `rect.center` ✗ —— 两者都量成画布中心 `(361, 256)` ✓，
        ///     因为交互物的根节点是**拉伸的全屏容器** ✓；
        ///  ③ **每张图尺寸完全一样、而且是透明底** ✓ —— 家具只占图里的一小块 ✓，
        ///     所以"矩形中心"根本不代表"家具在哪儿"✗ —— **必须看像素** ✓。
        ///
        /// 现在：取贴图的 **alpha 重心** ✓（透明处权重 0 ✓ → 出来的就是那件家具自己 ✓），
        /// 纹理像素 → Image 本地矩形 → 世界坐标 ✓。
        /// **贴图必须勾 Read/Write** ✓（否则 `GetPixels32` 会抛 ✗ → 这里退化成矩形中心 ✓ 并打一次警告 ✓）。
        /// </summary>
        private static Vector2 WorldCenter(SimpleInteractable interactable)
        {
            var id = interactable.GetInstanceID();
            if (CenterCache.TryGetValue(id, out var cached))
            {
                return cached;
            }

            var graphics = interactable.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            var sum = Vector2.zero;
            var count = 0;

            for (var i = 0; i < graphics.Length; i++)
            {
                var image = graphics[i] as UnityEngine.UI.Image;
                if (image == null || image.sprite == null)
                {
                    continue;
                }

                sum += SpriteAlphaCenter(image);
                count++;
            }

            var result = count > 0 ? sum / count : FallbackCenter(interactable);

            CenterCache[id] = result;
            return result;
        }

        /// <summary>贴图的 **alpha 重心** → 世界坐标 ✓（透明底不算 ✓，拿到的是家具自己 ✓）。</summary>
        private static Vector2 SpriteAlphaCenter(UnityEngine.UI.Image image)
        {
            var rect = image.rectTransform;
            var sprite = image.sprite;
            var texture = sprite != null ? sprite.texture : null;

            if (texture == null || !texture.isReadable)
            {
                WarnUnreadableOnce(texture);
                return rect.TransformPoint(rect.rect.center);
            }

            var spriteRect = sprite.textureRect;
            var sourceWidth = Mathf.Max(1, Mathf.RoundToInt(spriteRect.width));
            var sourceHeight = Mathf.Max(1, Mathf.RoundToInt(spriteRect.height));

            // **降采样** ✓：512 的图按 4 像素一格 ✓ —— 重心不需要像素级精度 ✓，但快几十倍 ✓。
            var step = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(sourceWidth, sourceHeight) / 96f));

            var pixels = texture.GetPixels32();
            var texWidth = texture.width;
            var texHeight = texture.height;
            var originX = Mathf.RoundToInt(spriteRect.x);
            var originY = Mathf.RoundToInt(spriteRect.y);

            double sumX = 0;
            double sumY = 0;
            double weight = 0;

            for (var y = 0; y < sourceHeight; y += step)
            {
                var py = originY + y;
                if (py < 0 || py >= texHeight)
                {
                    continue;
                }

                for (var x = 0; x < sourceWidth; x += step)
                {
                    var px = originX + x;
                    if (px < 0 || px >= texWidth)
                    {
                        continue;
                    }

                    // **只看 alpha** ✓：透明底那些像素权重 0 ✓ —— 这就是本轮问题的根因 ✓。
                    var alpha = pixels[(py * texWidth) + px].a / 255f;
                    if (alpha <= 0.05f)
                    {
                        continue;
                    }

                    sumX += px * alpha;
                    sumY += py * alpha;
                    weight += alpha;
                }
            }

            if (weight <= 0)
            {
                return rect.TransformPoint(rect.rect.center);
            }

            // 纹理像素坐标 → Image 本地矩形坐标 ✓（纹理 y 向上 ✓，sprite 不翻转时一致 ✓）→ 世界 ✓。
            var centerX = (float)(sumX / weight);
            var centerY = (float)(sumY / weight);

            var local = new Vector2(
                rect.rect.xMin + (((centerX - spriteRect.x) / spriteRect.width) * rect.rect.width),
                rect.rect.yMin + (((centerY - spriteRect.y) / spriteRect.height) * rect.rect.height));

            return rect.TransformPoint(local);
        }

        private static bool warnedUnreadable;

        private static void WarnUnreadableOnce(Texture texture)
        {
            if (warnedUnreadable)
            {
                return;
            }

            warnedUnreadable = true;
            Debug.LogWarning("[Case] 有家具贴图**不可读** ✗ —— 位置只能退化成矩形中心 ✓（那样衰减没意义 ✗）。"
                             + "请在贴图 Import Settings 里勾上 **Read/Write** ✓。"
                             + $"（第一张：{(texture != null ? texture.name : "空")}）");
        }

        /// <summary>一个带 sprite 的 Image 都没有时的兜底 ✓（纯透明热区那种 ✗）。</summary>
        private static Vector2 FallbackCenter(SimpleInteractable interactable)
        {
            var self = interactable.transform as RectTransform;
            return self != null ? self.TransformPoint(self.rect.center) : (Vector2)interactable.transform.position;
        }

        /// <summary>
        /// 挑本局的**主场** ✓（可以有 1~<see cref="maxOrigins"/> 个 ✓）：
        /// **调试字段填了就强制用填的那几件** ✓ —— 想复现"两个源离得很远"这种布局就用它 ✓；
        /// 没填就按种子抽 ✓（**各不相同** ✓）。
        /// </summary>
        private List<int> PickOrigins(IReadOnlyList<Target> targets, CaseRandom rng)
        {
            var result = new List<int>();

            if (debugForceOrigins != null && debugForceOrigins.Length > 0)
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    var id = targets[i].Interactable.InteractableId;

                    for (var k = 0; k < debugForceOrigins.Length; k++)
                    {
                        var keyword = debugForceOrigins[k];
                        if (!string.IsNullOrEmpty(keyword) && id.IndexOf(keyword, StringComparison.Ordinal) >= 0)
                        {
                            result.Add(i);
                            break;
                        }
                    }
                }

                if (result.Count > 0)
                {
                    Debug.Log("[Case] 主场由**调试字段**强制指定 ✓："
                              + string.Join(" / ", result.ConvertAll(index => targets[index].Interactable.InteractableId))
                              + "（清空 debugForceOrigins 才会按种子抽 ✓）");
                    return result;
                }

                Debug.LogWarning("[Case] debugForceOrigins 里没有一个关键词匹配上家具 id ✗ —— 这次仍然按种子抽 ✓。");
            }

            var wanted = Mathf.Clamp(rng.Next(1, Mathf.Max(1, maxOrigins) + 1), 1, targets.Count);
            var order = BuildShuffledOrder(targets.Count, rng);

            for (var i = 0; i < wanted && i < order.Count; i++)
            {
                result.Add(order[i]);
            }

            return result;
        }

        /// 把"离主场多远"折成**强度** ✓：主场自己 = 1 ✓，越远越低 ✓，超出半径就是 0 ✓。
        /// 再按种子给每件家具一点**抖动** ✓ —— 不然强度会是一圈圈规整的同心圆 ✗（一眼看穿 ✗）。
        /// </summary>
        private void ApplyAnomalyStrengths(
            IReadOnlyList<Target> targets, IReadOnlyList<int> origins, float[] strengths, CaseRandom rng)
        {
            var radius = ResolveDecayRadius(targets);

            for (var i = 0; i < targets.Count; i++)
            {
                var center = WorldCenter(targets[i].Interactable);

                // **主场就是 1.00，不抖** ✓ —— 抖动只用来打散"同距的几件" ✗，
                // 抖到主场头上会让日志里的 `★主场 0.89` ✗ 看着像没算对 ✓。
                if (IsOrigin(origins, i))
                {
                    strengths[i] = 1f;
                    continue;
                }

                // **取所有主场里最强的那一个** ✓：两个源的场叠在一起时 ✓，
                // 玩家读到的是"更近的那个源头"的读数 ✓（而不是两边加起来爆表 ✗）。
                var best = 0f;
                for (var o = 0; o < origins.Count; o++)
                {
                    var distance = Vector2.Distance(WorldCenter(targets[origins[o]].Interactable), center);
                    best = Mathf.Max(best, Mathf.Clamp01(1f - distance / radius));
                }

                // 抖动 ✓：同距的两件家具强度也会不一样 ✓（否则玩家拿尺子量距离就能反推 ✓）。
                // **乘在强度上** ✓（不是加 ✗）：远处那几件本来就在 0 附近 ✓，
                // 加一个固定值等于把它们整体抬起来 ✗ —— 实测出现过"最远两件 0.00 ✗、中间一件 0.05 ✗"
                // 这种根本不像梯度的分布 ✓，就是"加抖动 + 半径取一半"两件事叠出来的 ✓。
                var jitter = (float)((rng.NextDouble() * 2.0) - 1.0) * anomalyJitter;

                strengths[i] = Mathf.Clamp01(best * (1f + jitter));
            }
        }

        /// <summary>
        /// 衰减半径 ✓：手填了就用 ✓；**留 0 就按房间自己算** ✓。
        ///
        /// 自动值 = **离得最远那两件家具之间的真实距离** ✓（整个房间的跨度 ✓）——
        /// 于是主场 = 1.00 ✓、房间另一头 ≈ 0 ✓、中间大致 0.5 ✓，梯度铺满整个房间 ✓。
        ///
        /// ⚠ 这里踩过两个坑 ✗：① 系数取过一半 ✗（拍脑袋定的 ✓，结果只有紧贴主场的两件过门槛 ✗）；
        /// ② **`targets[j]` 那半边漏改了** ✗ —— 拿"真实中心"去和"画布中心 `(361,256)`"比距离 ✗，
        /// 算出来的半径和家具布局毫无关系 ✓（实测 ≈130 像素 ✗）。
        /// 以后改 `WorldCenter` 的取法时，**两个下标都要改** ✓。
        /// </summary>
        private float ResolveDecayRadius(IReadOnlyList<Target> targets)
        {
            if (anomalyDecayRadius > 0f || targets.Count < 2)
            {
                return anomalyDecayRadius > 0f ? anomalyDecayRadius : 1f;
            }

            var maxDistance = 0f;
            for (var i = 0; i < targets.Count; i++)
            {
                for (var j = i + 1; j < targets.Count; j++)
                {
                    maxDistance = Mathf.Max(maxDistance, Vector2.Distance(
                        WorldCenter(targets[i].Interactable),
                        WorldCenter(targets[j].Interactable)));
                }
            }

            return Mathf.Max(1f, maxDistance);
        }

        /// <summary>
        /// **诱饵** ✓：从"离主场最近"的几件里挑 1~2 件 ✓，把强度**清零** ✓ —— 它离源头近 ✓，
        /// 读数却比远处还干净 ✓（"近 = 有问题"这个直觉会被故意打脸 ✓）。
        ///
        /// 而且有几率**不出现** ✓：否则"每局都有一个近处诱饵"本身又变成规律了 ✗。
        /// </summary>
        private void PickDecoys(
            IReadOnlyList<Target> targets, IReadOnlyList<int> origins, float[] strengths, HashSet<int> decoySet, CaseRandom rng)
        {
            if (maxDecoys <= 0 || targets.Count < 3 || rng.NextDouble() > decoyChance)
            {
                return;
            }

            var order = BuildOrderByDistanceTo(targets, origins);
            var wanted = rng.Next(1, maxDecoys + 1);

            for (var i = 0; i < order.Count && decoySet.Count < wanted; i++)
            {
                var index = order[i];
                if (IsOrigin(origins, index))
                {
                    continue; // 主场自己不能当诱饵 ✗
                }

                strengths[index] = 0f;
                decoySet.Add(index);
            }
        }

        /// <summary>按"离**最近的那个主场**从近到远"排 ✓（诱饵就从最近那几件里挑 ✓）。</summary>
        private static List<int> BuildOrderByDistanceTo(IReadOnlyList<Target> targets, IReadOnlyList<int> origins)
        {
            var order = new List<int>(targets.Count);

            for (var i = 0; i < targets.Count; i++)
            {
                order.Add(i);
            }

            order.Sort((a, b) => DistanceToOrigins(targets, origins, a)
                .CompareTo(DistanceToOrigins(targets, origins, b)));

            return order;
        }

        /// <summary>
        /// 这个下标是不是主场之一 ✓。
        /// **不用 `origins.Contains`** ✗：`IReadOnlyList<int>` 没有这个方法 ✓，
        /// 而这个文件没引 `System.Linq` ✓（一直靠 `List` 自带的方法 ✓），
        /// 硬写会掉进 `MemoryExtensions.Contains` 那个 span 重载 ✗ → CS7036 ✓。
        /// </summary>
        private static bool IsOrigin(IReadOnlyList<int> origins, int index)
        {
            for (var i = 0; i < origins.Count; i++)
            {
                if (origins[i] == index)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>到**最近那个主场**的距离 ✓（多主场时按最近的算 ✓）。</summary>
        private static float DistanceToOrigins(IReadOnlyList<Target> targets, IReadOnlyList<int> origins, int index)
        {
            var center = WorldCenter(targets[index].Interactable);
            var best = float.MaxValue;

            for (var o = 0; o < origins.Count; o++)
            {
                best = Mathf.Min(best, Vector2.Distance(WorldCenter(targets[origins[o]].Interactable), center));
            }

            return best == float.MaxValue ? 0f : best;
        }


        /// <summary>
        /// 从 <see cref="InventoryManager"/> 的全局 Item 表里按 id 找收容物 ✓ ——
        /// 这样建造工具不用再往 CaseDirector 上连一遍收容物引用 ✓（少一处会漏连的地方 ✓）。
        /// </summary>
        private static Project.Gameplay.Scripts.Items.ClueItem FindClueById(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !Services.TryGet<InventoryManager>(out var inventory))
            {
                return null;
            }

            var source = inventory.ItemSource;
            if (source == null)
            {
                return null;
            }

            foreach (var item in source)
            {
                if (item is Project.Gameplay.Scripts.Items.ClueItem clue && clue.ItemId == itemId)
                {
                    return clue;
                }
            }

            return null;
        }

        /// <summary>
        /// 规则读出一条读数后叫它 ✓：**每条读数都记账** ✓（这件家具读了几条 ✓、其中几条异常 ✓）。
        ///
        /// 但**证据仍按"一处产地算一条"** ✓（<see cref="CaseSpec.Read"/> ✓）：拿五把工具把同一件家具读穿 ✓
        /// 不会把判定门槛灌水 ✗，只会让你**更确定**✓ —— 这正是"多份读数互相印证"的意思 ✓。
        /// </summary>
        public void NotifyRead(string interactableId, bool readingIsAnomaly = false, string toolName = null, string text = null)
        {
            if (submitted || string.IsNullOrEmpty(interactableId))
            {
                return;
            }

            if (!specs.TryGetValue(interactableId, out var spec))
            {
                return;
            }

            // 读数账本 ✓：每条都记 ✓（同一把工具反复读就反复记 ✓，没有任何副作用 ✓）。
            spec.ReadingsTaken++;
            if (readingIsAnomaly)
            {
                spec.AnomalousReadings++;
            }

            var firstRead = !spec.Read;
            if (firstRead)
            {
                spec.Read = true;
                readOrder.Add(interactableId);
            }

            specs[interactableId] = spec; // CaseSpec 是结构体 ✓：改完必须写回 ✓

            if (firstRead && spec.IsAnomaly)
            {
                foundAnomalies++;

                // 异常读数才算"证据"：EvidenceManager 会顺手刷新 HUD 的读数行，够数时自己发 flag。
                if (Services.TryGet<EvidenceManager>(out var evidenceManager))
                {
                    evidenceManager.AddEvidence($"anomaly_{interactableId}");
                }
            }

            if (logCase)
            {
                var readingLine = string.IsNullOrEmpty(toolName)
                    ? string.Empty
                    : $" ← {toolName}「{text}」（{(readingIsAnomaly ? "异常" : "正常")}）";

                Debug.Log($"[Case] 读数：{spec.DisplayName}{readingLine}"
                          + $"；这件已读 {spec.ReadingsTaken} 条（异常 {spec.AnomalousReadings} 条）"
                          + $" → 证据 {foundAnomalies}/{corroborationNeeded}，已读产地 {readOrder.Count} 处");
            }
        }

        /// <summary>
        /// 玩家下结论。<paramref name="saidFakeHuman"/> = 他判"这是伪人"。
        /// 判对 → Victory，判错 → GameOver；读数不够互相印证时也允许交，那就是赌。
        /// </summary>
        public void SubmitVerdict(bool saidFakeHuman)
        {
            if (submitted || !HasCase)
            {
                return;
            }

            submitted = true;

            var correct = saidFakeHuman == isFakeHuman;
            var enough = IsEnoughToConclude;

            var verdictText = saidFakeHuman ? "伪人" : "正常人";
            var resultText = correct
                ? $"结论：{verdictText} —— 判断正确。（异常读数 {foundAnomalies} 条）"
                : $"结论：{verdictText} —— 判断错误。它其实是{(isFakeHuman ? "伪人" : "普通人")}。";

            if (correct && !enough)
            {
                resultText += " 读数还不够互相印证，你赌对了。";
            }

            LastResultText = resultText;
            LastVerdictCorrect = correct;

            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.ShowToolResult(resultText, true);
                uiManager.ShowHint(
                    enough ? "读数互相印证，结论成立。" : "读数还不够互相印证，这个结论是赌的。",
                    4f);
            }

            // 结算交给游戏循环：判对 → Victory，判错 → GameOver。
            if (Services.TryGet<GameLoopManager>(out var gameLoopManager))
            {
                gameLoopManager.ResolveCase(correct);
            }
            else if (Services.TryGet<GameManager>(out var gameManager))
            {
                // 兜底：场景里没有 GameLoopManager 时直接把状态落下去，别把流程卡住。
                if (correct)
                {
                    gameManager.TriggerVictory().Forget();
                }
                else
                {
                    gameManager.TriggerGameOver().Forget();
                }
            }

            if (logCase)
            {
                Debug.Log($"[Case] 提交结论：玩家判「{verdictText}」，正确答案「{(isFakeHuman ? "伪人" : "正常人")}」，"
                          + $"正确={correct}，异常 {foundAnomalies}/{corroborationNeeded}（种子 {seed}）");
            }
        }

        // ---------- 内部 ----------

        private readonly struct Target
        {
            public readonly SampleInteractableRule Rule;
            public readonly SimpleInteractable Interactable;

            public Target(SampleInteractableRule rule, SimpleInteractable interactable)
            {
                Rule = rule;
                Interactable = interactable;
            }
        }

        private static List<Target> FindTargets()
        {
            var result = new List<Target>();
            var rules = FindObjectsByType<SampleInteractableRule>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var rule in rules)
            {
                if (rule == null)
                {
                    continue;
                }

                var interactable = rule.GetComponent<SimpleInteractable>();
                if (interactable != null)
                {
                    result.Add(new Target(rule, interactable));
                }
            }

            // 按 id 排序：同一颗种子必须摇出同一份案情，所以顺序不能依赖场景里的遍历顺序。
            result.Sort((a, b) => string.CompareOrdinal(a.Interactable.InteractableId, b.Interactable.InteractableId));
            return result;
        }

        private static List<int> BuildShuffledOrder(int count, System.Random rng)
        {
            var order = new List<int>(count);
            for (var i = 0; i < count; i++)
            {
                order.Add(i);
            }

            // Fisher-Yates
            for (var i = order.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            return order;
        }

        private static CaseReadingKind PickKind(System.Random rng, List<CaseReadingKind> available)
        {
            return available[rng.Next(available.Count)];
        }

        /// <summary>本局带的工具能读出哪几种读数（每局只从这里面挑）。</summary>
        private static List<CaseReadingKind> BuildAvailableKinds(List<ToolItem> loadout)
        {
            var kinds = new List<CaseReadingKind>();
            if (loadout.Count == 0)
            {
                // 没配工具池：退回"四种读数都能用"，和换装备之前的行为一致。
                kinds.Add(CaseReadingKind.Temperature);
                kinds.Add(CaseReadingKind.Emf);
                kinds.Add(CaseReadingKind.Uv);
                kinds.Add(CaseReadingKind.Audio);
                return kinds;
            }

            foreach (var tool in loadout)
            {
                var kind = KindOf(tool != null ? tool.ToolType : ToolType.None);
                if (kind != CaseReadingKind.None && !kinds.Contains(kind))
                {
                    kinds.Add(kind);
                }
            }

            return kinds;
        }

        /// <summary>工具 → 读数类型。</summary>
        private static CaseReadingKind KindOf(ToolType type)
        {
            switch (type)
            {
                case ToolType.ToolKit: return CaseReadingKind.Physical;
                case ToolType.UVLight: return CaseReadingKind.Uv;
                case ToolType.Detector: return CaseReadingKind.Emf;
                case ToolType.Thermometer: return CaseReadingKind.Temperature;
                case ToolType.Recorder: return CaseReadingKind.Audio;
                default: return CaseReadingKind.None;
            }
        }

        /// <summary>从工具池里选出本局实际带的几件（装备位几格就带几件，多余的一件随机不带）。</summary>
        private void BuildLoadout(System.Random rng)
        {
            equippedTools.Clear();
            if (loadoutPool == null || loadoutPool.Length == 0)
            {
                return;
            }

            var pool = new List<ToolItem>();
            foreach (var tool in loadoutPool)
            {
                if (tool != null)
                {
                    pool.Add(tool);
                }
            }

            if (pool.Count == 0)
            {
                return;
            }

            var capacity = Services.TryGet<InventoryManager>(out var inventoryManager)
                ? inventoryManager.EquipmentCapacity
                : pool.Count;
            var keep = Mathf.Clamp(capacity, 1, pool.Count);
            var dropIndex = pool.Count > keep ? rng.Next(pool.Count) : -1;

            for (var i = 0; i < pool.Count; i++)
            {
                if (i == dropIndex)
                {
                    continue;
                }

                if (equippedTools.Count >= keep)
                {
                    break;
                }

                equippedTools.Add(pool[i]);
            }
        }

        private static ToolType ToolOf(CaseReadingKind kind)
        {
            switch (kind)
            {
                case CaseReadingKind.Physical: return ToolType.ToolKit;
                case CaseReadingKind.Temperature: return ToolType.Thermometer;
                case CaseReadingKind.Emf: return ToolType.Detector;
                case CaseReadingKind.Uv: return ToolType.UVLight;
                case CaseReadingKind.Audio: return ToolType.Recorder;
                default: return ToolType.None;
            }
        }

        private CaseSpec BuildSpec(System.Random rng, SimpleInteractable interactable, CaseReadingKind kind, bool isAnomaly)
        {
            var spec = new CaseSpec
            {
                InteractableId = interactable.InteractableId,
                DisplayName = interactable.InteractableId,
                Kind = kind,
                Tool = ToolOf(kind),
                IsAnomaly = isAnomaly,
            };

            switch (kind)
            {
                case CaseReadingKind.Temperature:
                    spec.Temperature = NextFloat(rng, isAnomaly ? anomalyTemperature : normalTemperature);
                    spec.SuccessText = $"红外温度计：{spec.Temperature:0.#}℃ —— "
                                       + (isAnomaly ? "比室温低得不对劲。" : "和室温差不多。");
                    break;
                case CaseReadingKind.Emf:
                    spec.Emf = Mathf.RoundToInt(NextFloat(rng, isAnomaly ? anomalyEmf : normalEmf));
                    spec.SuccessText = $"便携探测器：EMF {spec.Emf} 级 —— "
                                       + (isAnomaly ? "读数高得不像正常电器。" : "在正常范围内。");
                    break;
                case CaseReadingKind.Uv:
                    spec.Uv = isAnomaly;
                    spec.SuccessText = isAnomaly
                        ? "紫外线灯：照出了不该有的荧光痕迹。"
                        : "紫外线灯：没有任何荧光反应。";
                    break;
                case CaseReadingKind.Audio:
                    spec.Audio = isAnomaly ? AudioType.Scream : AudioType.Normal;
                    spec.SuccessText = isAnomaly
                        ? "录音笔：回放出一段不属于这个房间的嘶吼声。"
                        : "录音笔：只有一段平稳的呼吸声。";
                    break;
                case CaseReadingKind.Physical:
                    spec.SuccessText = isAnomaly
                        ? "工具包：撬开夹层，里面塞着不属于这栋房子的东西。"
                        : "工具包：拆开看过了，里面很正常。";
                    break;
                default:
                    spec.SuccessText = "没有读数。";
                    break;
            }

            return spec;
        }

        private static float NextFloat(System.Random rng, Vector2 range)
        {
            return Mathf.Lerp(range.x, range.y, (float)rng.NextDouble());
        }
    }
}
