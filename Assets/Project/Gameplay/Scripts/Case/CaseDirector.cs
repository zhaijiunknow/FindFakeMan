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

        /// <summary>这条读数是不是异常（伪人局的"真的有问题"那些家具为 true）。</summary>
        public bool IsAnomaly;

        /// <summary>玩家是否已经读过它（同一件只算一次）。</summary>
        public bool Read;

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

        [Header("装备")]
        [Tooltip("可带的工具池（5 件）。每局从这里随机少带一件 —— 装备位有几格就带几件；留空 = 不换装备。")]
        [SerializeField] private ToolItem[] loadoutPool = new ToolItem[0];

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

            return text.ToString();
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

            var targets = FindTargets();
            if (targets.Count == 0)
            {
                Debug.LogWarning("[Case] 场景里没有找到任何交互物，案情没有生成。");
                return;
            }

            // 哪几件"真的有异常"：只对伪人局有意义。正常人局全部是正常读数，
            // 玩家应该读到"哪儿都没问题"再下「正常人」的结论。
            var anomalySet = new HashSet<int>();
            if (isFakeHuman)
            {
                var upper = Mathf.Clamp(maxAnomalyTargets, 1, targets.Count);
                var lower = Mathf.Clamp(minAnomalyTargets, 1, upper);
                var anomalyCount = rng.Next(lower, upper + 1);

                var order = BuildShuffledOrder(targets.Count, rng);
                for (var i = 0; i < anomalyCount && i < order.Count; i++)
                {
                    anomalySet.Add(order[i]);
                }
            }

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

                specs[id] = spec;
                rule.ConfigureFromCase(spec, inspectionRequiredChance > 0f && furnitureRng.NextDouble() < inspectionRequiredChance);
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

                Debug.Log($"[Case] 本局案情已生成：种子 {seed}，身份 {(isFakeHuman ? "伪人" : "正常人")}，"
                          + $"家具 {targets.Count} 件，其中异常 {anomalySet.Count} 件，需要 {corroborationNeeded} 条互相印证；"
                          + $"带进场：{loadoutText}。");
            }
        }

        /// <summary>规则读完一条观测后叫它：同一件只算一次，异常就 +1 并记进证据。</summary>
        public void NotifyRead(string interactableId)
        {
            if (submitted || string.IsNullOrEmpty(interactableId))
            {
                return;
            }

            if (!specs.TryGetValue(interactableId, out var spec) || spec.Read)
            {
                return;
            }

            spec.Read = true;
            specs[interactableId] = spec;
            readOrder.Add(interactableId);

            if (spec.IsAnomaly)
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
                Debug.Log($"[Case] 读到观测：{interactableId}（{spec.Kind}，异常={spec.IsAnomaly}）"
                          + $"→ 异常 {foundAnomalies}/{corroborationNeeded}，已读 {readOrder.Count} 件");
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
