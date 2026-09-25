using System;
using System.Collections.Generic;
using Project.Core.Runtime.Framework;
// **别名必须起** ✗→✓：Unity 自己也有 `UnityEngine.AudioType` ✓（本文件用了 `using UnityEngine;` ✓），
// 裸写 `AudioType` 直接 CS0104 二义 ✗ —— 这个文件里一律指**项目那个** ✓
//（原来的字段声明靠全限定名躲过去了 ✗，换成别名统一收口 ✓）。
using AudioType = Project.Core.Runtime.Framework.AudioType;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Case;
using Project.Gameplay.Scripts.Items;
using UnityEngine;

namespace Project.Gameplay.Scripts.Interactables
{
    [RequireComponent(typeof(SimpleInteractable))]
    public sealed class SampleInteractableRule : MonoBehaviour
    {
        [SerializeField] private bool resolveOnClick;
        [SerializeField] private ToolType requiredToolType;
        [SerializeField] private ClueItem clueItem;
        [SerializeField] private string evidenceId;

        // 本局的收容物：先挂在"待解锁"上 ✓，读过一次才交给玩家 ✓（见 AttachClue ✓）。
        private ClueItem pendingClue;
        private bool pendingClueToContainment;

        /// <summary>
        /// 这里**已经读过第一条读数**了 ✓ —— 收集 / 证据 / 标已查 / 解锁拾取**只做这一次** ✓。
        /// 之后换别的工具再读 ✓ 只是"再来一条读数" ✓（同一个产地不该被反复收集 ✗）。
        /// </summary>
        private bool unlockedByReading;
        [SerializeField] private bool collectToContainment;
        [SerializeField] private bool deactivateOnSuccess = true;
        [SerializeField] private bool markCollectedOnSuccess = true;
        [SerializeField] private string successFlag;
        [SerializeField] private string successText = "已完成调查。";
        [SerializeField] private string failureText = "当前工具无法处理该线索。";
        [SerializeField] private int failureSanityPenalty;
        [SerializeField] private bool registerTemperature;
        [SerializeField] private float temperatureValue;
        [SerializeField] private bool registerEmf;
        [SerializeField] private int emfValue;
        [SerializeField] private bool registerUv;
        [SerializeField] private bool uvValue;
        [SerializeField] private bool registerAudio;
        [SerializeField] private Project.Core.Runtime.Framework.AudioType audioType;

        [Header("次要工具（设计表里 ToolEffectEntry 列表的临时简化：一件家具暂时支持两把工具）")]
        [Tooltip("第二把能作用在这里的工具。None = 没有。它只做观测（读数 + flag），不拾取、不消耗收集流程。")]
        [SerializeField] private ToolType secondaryToolType = ToolType.None;
        [Tooltip("用次要工具成功时的读数文案，例如「温度：8℃」。")]
        [SerializeField] private string secondarySuccessText;
        [Tooltip("用次要工具成功后写入的 flag。")]
        [SerializeField] private string secondaryFlag;
        [Tooltip("用次要工具时登记的「温度」观测值（BranchManager.RegisterTemperature）。")]
        [SerializeField] private float secondaryTemperatureValue;

        [Header("读数条目（一件家具可以挂**多把**工具 ✓，每把各给一条自己的读数 ✓）")]
        [Tooltip("按 Docs/ContainmentRules.md §5.3-1 的口径 ✓：一串「工具 → 读数 → 文案」✓。\n"
                 + "留空 = 用上面的老字段自动拼出条目 ✓（场景里已烘好的数据不用重建 ✓）。\n"
                 + "**每条读数一律平等** ✓：每条都带着自己的「正常 / 异常」✓（IsAnomaly ✓），\n"
                 + "判定靠多条互相印证 ✓；收集 / 解锁拾取只做**第一条**读数 ✓。")]
        [SerializeField] private ToolReading[] toolReadings = new ToolReading[0];

        /// <summary>
        /// 一条读数：**哪把工具** ✓ → **读到什么** ✓ → **说什么文案** ✓。
        ///
        /// 为什么要成串 ✗→✓：一件家具经常有多把对口工具 ✓（书架既能量温度 ✓ 又能录音 ✓；
        /// 沙发既能录音 ✓ 又能用探测器出 CG ✓）—— 这是设计表 §3.3 的原始口径 ✓，
        /// 旧的 `requiredToolType + secondaryToolType` 只能凑两把 ✗，且次要那把是**残的** ✗。
        /// </summary>
        [Serializable]
        public struct ToolReading
        {
            /// <summary>对口工具（None = 空手 ✓）。</summary>
            public ToolType Tool;

            /// <summary>
            /// 日志 / 提示里显示的工具名 ✓（「温度枪」✓）—— **由读数种类表带过来** ✓，
            /// 不再在代码里 switch ✗（那样加新读数就得多改一处 ✗）。
            /// 场景里烘好的老数据没有它 ✗ → 回落到按 <see cref="ToolType"/> 猜 ✓。
            /// </summary>
            public string ToolDisplayName;

            /// <summary>这条读数的种类 ✓（决定登记到 BranchManager 的哪个观测位 ✓）。</summary>
            public CaseReadingKind Kind;

            public float Temperature;
            public int Emf;
            public bool Uv;
            public AudioType Audio;

            /// <summary>读数文案（详情区 / 结果提示显示的就是它 ✓）。</summary>
            public string Text;

            /// <summary>读到之后写入的 flag（可空 ✓）。</summary>
            public string Flag;

            /// <summary>
            /// **这条读数本身是不是异常的** ✓ —— 判定靠"多条读数互相印证" ✓，不靠"哪条是主"✗。
            ///
            /// 同一件家具上会混着正常与异常 ✓：异常家具**未必条条都异常** ✓（那太直白 ✗），
            /// 玩家要自己权衡"这条只是环境噪声 ✓、那条才是真的不对"✓ —— 这就是这套玩法的推理部分 ✓。
            /// </summary>
            public bool IsAnomaly;
        }

        [Header("门槛")]
        [Tooltip("勾上后必须先「检视 / 放大看清」这件东西，工具才生效（木桌抽屉：不放大看不清里面有什么）。")]
        [SerializeField] private bool requiresInspection;
        [Tooltip("没先检视就上工具时的提示。不扣 SAN —— 这是流程门槛，不是用错工具。")]
        [SerializeField] private string inspectionRequiredText = "先放大看清这里，再用工具。";

        private SimpleInteractable interactable;

        public bool ResolveOnClick => resolveOnClick;

        /// <summary>这个交互物需要的工具类型（None = 空手就能处理）。拖拽时的"能不能用"提示要用它。</summary>
        public ToolType RequiredToolType => requiredToolType;

        /// <summary>是不是"必须先检视"才能上工具。</summary>
        public bool RequiresInspection => requiresInspection;

        /// <summary>
        /// 玩家做过「检视」（右拖）吗 —— 也就是 <c>requiresInspection</c> 那道门槛看的东西。
        /// 注意用的是 <see cref="SimpleInteractable.IsZoomed"/> 而不是单纯"面板开着"：
        /// 点一下只是选中，真正"看清了"是右拖检视那个动作。
        /// </summary>
        private bool IsInspected => interactable != null && interactable.IsZoomed;

        /// <summary>
        /// 由 <see cref="CaseDirector"/> 在开局灌入本局的读数（通用关卡器：内容不再写死在场景里，
        /// 场景只负责"房间长什么样"，"这一局这里能用什么工具、读到什么"是运行时摇出来的）。
        ///
        /// 读数型交互的口径：工具对口 → 读出观测值并回报给 CaseDirector；
        /// **不拾取、不进收容、不消耗证据流程、不失效**（同一件可以反复读，证据由 CaseDirector 记）。
        /// </summary>
        public void ConfigureFromCase(CaseSpec spec, bool requireInspection)
        {
            requiredToolType = spec.Tool;
            resolveOnClick = false;
            clueItem = null;
            unlockedByReading = false;
            evidenceId = string.Empty;
            collectToContainment = false;
            deactivateOnSuccess = false;
            markCollectedOnSuccess = false;
            successFlag = string.Empty;
            failureSanityPenalty = 0;
            requiresInspection = requireInspection;
            inspectionRequiredText = "先点开看清这里，再用工具。";

            registerTemperature = spec.Kind == CaseReadingKind.Temperature;
            temperatureValue = spec.Temperature;
            registerEmf = spec.Kind == CaseReadingKind.Emf;
            emfValue = spec.Emf;
            registerUv = spec.Kind == CaseReadingKind.Uv;
            uvValue = spec.Uv;
            registerAudio = spec.Kind == CaseReadingKind.Audio;
            audioType = spec.Audio;

            secondaryToolType = ToolType.None;
            secondarySuccessText = string.Empty;
            secondaryFlag = string.Empty;

            // 案情只摇出**一条**主读数 ✓（异常 / 正常就靠它判 ✓）；副读数由 CaseDirector
            // 用 AddReading 补挂 ✓（比如书架再给一条录音 ✓）—— 所以这里重建整串 ✓，
            // 别让上一局残留的条目跟着下一局跑 ✗。
            toolReadings = new[]
            {
                new ToolReading
                {
                    Tool = spec.Tool,
                    Kind = spec.Kind,
                    Temperature = spec.Temperature,
                    Emf = spec.Emf,
                    Uv = spec.Uv,
                    Audio = spec.Audio,
                    Text = string.IsNullOrEmpty(spec.SuccessText) ? "读数已记录。" : spec.SuccessText,
                    Flag = string.Empty,
                    IsAnomaly = spec.IsAnomaly,
                },
            };

            successText = string.IsNullOrEmpty(spec.SuccessText) ? "读数已记录。" : spec.SuccessText;
            failureText = "这把工具在这里读不到东西。";
        }

        /// <summary>
        /// 把这个交互点变成某件**收容物**的产地 ✓（由 CaseDirector 开局按固定产出表调 ✓）。
        ///
        /// 口径见 Docs/ContainmentRules.md §1：
        ///  - 玩家先用对口工具读一次 → 那个动作用 <c>markCollectedOnSuccess</c> 把它标成「已查」✓；
        ///  - **「拾取」要等这一步之后才解锁** ✓（没查过就乱收，箱子 3 格根本不够用 ✗）；
        ///  - 读出来的东西走收容箱 ✓（<c>collectToContainment = true</c> ✓），不是普通背包 ✗；
        ///  - <c>deactivateOnSuccess = false</c> ✓：读完这件家具还能继续读别的读数 ✓，不会因为掉了线索就失效 ✗。
        /// </summary>
        public void AttachClue(ClueItem clue, string evidence, bool toContainment)
        {
            // 注意：**不写进 clueItem** ✗ —— clueItem 那条路是"读数成功就自动进箱"（ApplyCollection ✓），
            // 而你的口径是"读过之后**解锁拾取**，由玩家决定收不收"✓（3 格根本不够乱收 ✗）。
            // 所以收容物先挂在这条待解锁的链上 ✓，读过一次才交到玩家手上 ✓。
            pendingClue = clue;
            pendingClueToContainment = toContainment;
            unlockedByReading = false;
            clueItem = null;

            evidenceId = evidence ?? string.Empty;
            collectToContainment = toContainment;
            markCollectedOnSuccess = true;   // 读过一次 = 已查 ✓ → 解锁拾取 ✓
            deactivateOnSuccess = false;     // 读完还能继续读别的读数 ✓
        }

        /// <summary>本局这个产地的收容物要不要进收容箱 ✓（正常收容物也要进 ✓ —— 见 Docs/ContainmentRules.md §1）。</summary>
        public bool CollectsToContainment => pendingClueToContainment || collectToContainment;

        /// <summary>
        /// 读过一次之后，把收容物交到玩家手上 ✓ —— 从此点这个交互物，详情区显示的就是它 ✓，
        /// 体感上的「拾取」也才跟着可用 ✓（这是"解锁拾取"那一下 ✓）。
        /// </summary>
        private void RevealPendingClue()
        {
            if (pendingClue == null || interactable == null)
            {
                return;
            }

            interactable.SetRuntimeItem(pendingClue);

            // 诊断日志 ✓：这是"解锁拾取"的唯一时刻 ✓，而它太容易被漏看 ✗
            //（面板会在同一帧被 ShowSuccess 关掉 ✓）—— 所以明确打一行 ✓，
            // 以后判断"这处到底有没有收容物"直接搜 `收容物已解锁` 就行 ✓。
            Debug.Log($"[Case] 收容物已解锁：{interactable.InteractableId} → {pendingClue.DisplayName}"
                      + $"（{pendingClue.ItemId}，现在可以「拾取」✓）");
        }

        /// <summary>
        /// 收容物被拿走了 ✓ → 这里**再也不给** ✓（丢弃之后同样找不回来 ✓，见 Docs/ContainmentRules.md §1）。
        /// 由 <see cref="Project.Gameplay.Scripts.Items.ItemActionRunner"/> 拾取成功时调 ✓。
        /// </summary>
        public void MarkClueTaken()
        {
            pendingClue = null;
            interactable?.SetRuntimeItem(null);
        }

        /// <summary>
        /// 给定工具能不能正确处理这里（只看类型和耐久，**不消耗耐久也不产生任何副作用**）。
        /// InteractionManager.CanUseToolOn 只判断"能不能尝试"，工具是否对口要问规则，所以单独提供这个查询。
        /// </summary>
        public bool CanUseTool(ToolItem toolItem)
        {
            if (toolItem == null)
            {
                return false;
            }

            // 门槛：得先在检视里看清它，工具才起作用。
            // 这里返回 false，拖拽时的"能不能用"提示会跟着变红 —— 等于把"先点开看"教给玩家。
            if (requiresInspection && !IsInspected)
            {
                return false;
            }

            // ⚠️ **不再要求"工具对口"** ✗→✓ —— 对标恐鬼症：任何工具都能对着任何地方用 ✓，
            // 用不用得出东西交给 Resolve 决定 ✓（不对口 = "这里没有它能读的东西"✓，没有惩罚 ✗）。
            // 剩下的唯一硬条件是"还有耐久"✓（工具包 5 点 = 能用 5 次 ✓）。
            return toolItem.Durability > 0;
        }

        /// <summary>
        /// ⚠ **已废弃** ✗：现在走 <see cref="ToolReading"/> 条目那条路 ✓（老字段由 <see cref="EnsureReadings"/> 拼成条目 ✓），
        /// 这里没有任何调用点了 ✓ —— 留着只为对照历史口径 ✗，别再往里加东西 ✓。
        ///
        /// 次要工具分支：只登记观测 + 读数，**不做拾取、不进收容、不消耗证据流程**。
        /// 对应设计表花坛手表模板里"同一件家具先用温度计读温度、再用探测器确认"的那种用法。
        /// </summary>
        private bool ResolveSecondary(ToolItem toolItem)
        {
            if (!toolItem.Use())
            {
                Fail();
                return false;
            }

            if (Services.TryGet<BranchManager>(out var branchManager) && interactable != null)
            {
                branchManager.RegisterTemperature(interactable.InteractableId, secondaryTemperatureValue);
            }

            if (!string.IsNullOrWhiteSpace(secondaryFlag) && Services.TryGet<FlagManager>(out var flagManager))
            {
                flagManager.Set(secondaryFlag);
            }

            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.ShowToolResult(
                    string.IsNullOrWhiteSpace(secondarySuccessText) ? "读数已记录。" : secondarySuccessText,
                    true);
            }

            return true;
        }

        /// <summary>
        /// 工具不对口时走这里 ✓：**用得出来，只是这片区域没有它能读的东西** ✓。
        ///
        /// 为什么不再惩罚 ✗：对标恐鬼症 —— 什么工具都能对着任何地方用 ✓，读出什么才是内容 ✓。
        /// 所以这条路：照常消耗耐久 ✓、把"没读到"说出来 ✓，但
        /// **不扣 SAN** ✓、**不算已查** ✓（不 SetCollected ✓）、**不解锁拾取** ✓（不 RevealPendingClue ✓）、
        /// 也不收面板 ✓ —— 玩家可以接着换别的工具试 ✓，这正是"多把道具各给一条读数"的基础 ✓。
        /// </summary>
        private bool ResolveOffTopic(ToolItem toolItem)
        {
            toolItem.Use();

            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.ShowToolResult(failureText, false);
            }

            return true;
        }

        // ── 读数条目：一件家具 × 多把工具 ✓ ──────────────────────────────────────

        /// <summary>
        /// 再挂一条读数 ✓（由 <see cref="CaseDirector"/> 开局调 ✓ —— 比如书架除了温度再补一条录音 ✓）。
        ///
        /// **同一种工具重复挂 = 后挂的覆盖前一条** ✓：一把工具在这里只能有一条读数 ✗
        ///（否则同一个动作冒出两条文案 ✓ 玩家会懵 ✗）。
        /// </summary>
        public void AddReading(ToolReading reading)
        {
            EnsureReadings();

            var list = new List<ToolReading>(toolReadings);
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Tool == reading.Tool)
                {
                    list[i] = reading;
                    toolReadings = list.ToArray();
                    return;
                }
            }

            list.Add(reading);
            toolReadings = list.ToArray();
        }

        /// <summary>
        /// 把老的 <c>requiredToolType</c> / <c>secondaryToolType</c> 拼成条目 ✓ ——
        /// 这样场景里已经烘好的数据**不用重建** ✓，跑起来立刻就有"一串读数"的能力 ✓。
        /// </summary>
        private void EnsureReadings()
        {
            if (toolReadings != null && toolReadings.Length > 0)
            {
                return;
            }

            // 老字段里"登记哪一种观测"是四个 bool ✓ → 反推出种类 ✓（同时只会有一种 ✓）。
            var kind = CaseReadingKind.None;
            if (registerTemperature)
            {
                kind = CaseReadingKind.Temperature;
            }
            else if (registerEmf)
            {
                kind = CaseReadingKind.Emf;
            }
            else if (registerUv)
            {
                kind = CaseReadingKind.Uv;
            }
            else if (registerAudio)
            {
                kind = CaseReadingKind.Audio;
            }

            var list = new List<ToolReading>(2)
            {
                new ToolReading
                {
                    Tool = requiredToolType,
                    ToolDisplayName = DescribeTool(requiredToolType),
                    Kind = kind,
                    Temperature = temperatureValue,
                    Emf = emfValue,
                    Uv = uvValue,
                    Audio = audioType,
                    Text = string.IsNullOrWhiteSpace(successText) ? "读数已记录。" : successText,
                    Flag = successFlag,
                    IsAnomaly = IsAnomalousReading(kind, temperatureValue, emfValue, uvValue, audioType),
                },
            };

            if (secondaryToolType != ToolType.None)
            {
                list.Add(new ToolReading
                {
                    Tool = secondaryToolType,
                    ToolDisplayName = DescribeTool(secondaryToolType),
                    Kind = CaseReadingKind.Temperature,
                    Temperature = secondaryTemperatureValue,
                    Text = string.IsNullOrWhiteSpace(secondarySuccessText) ? "读数已记录。" : secondarySuccessText,
                    Flag = secondaryFlag,
                    IsAnomaly = secondaryTemperatureValue <= AnomalyTemperatureThreshold,
                });
            }

            toolReadings = list.ToArray();
        }

        /// <summary>这把工具在这里有没有读数 ✓（空手 = 查 <see cref="ToolType.None"/> 那条 ✓）。</summary>
        private ToolReading? FindReading(ToolType toolType)
        {
            EnsureReadings();

            for (var i = 0; i < toolReadings.Length; i++)
            {
                if (toolReadings[i].Tool == toolType)
                {
                    return toolReadings[i];
                }
            }

            return null;
        }

        /// <summary>
        /// 走一条读数 ✓：耗耐久 ✓ → 登记观测 ✓ → 写 flag ✓ → 报案情 ✓ → 说文案 ✓。
        ///
        /// **每条读数一律平等** ✓（✗ 早先分过"主 / 副" ✗）：一件家具上五把工具各给一条自己的读数 ✓，
        /// 有的显示正常 ✓、有的显示异常 ✓ —— 异常与否**由读数自己带着** ✓（<see cref="ToolReading.IsAnomaly"/> ✓），
        /// 判定是"多条互相印证"的事 ✓，不是"哪条在主"的事 ✗。
        ///
        /// 面板**不关** ✓：换一把工具接着读 ✓ 才是这套玩法的骨架 ✓
        ///（以前读一条就把面板收掉 ✗ = 每读一条都要重新选中一遍 ✗）。
        /// 收集 / 解锁拾取只做**第一条** ✓（它就算"查过这里"✓），之后换工具纯粹是再读一条 ✓。
        /// </summary>
        private bool ResolveReading(ToolReading reading, ToolItem toolItem)
        {
            if (reading.Tool != ToolType.None)
            {
                if (toolItem == null || !toolItem.Use())
                {
                    Fail();
                    return false;
                }
            }

            RegisterObservation(reading);

            // 探测器读到 **5 级** = 检测异常 ✓ → 出 CG（见 Docs/ContainmentRules.md §2.1 ✓）。
            // 判定看**这条读数** ✓ —— 哪把工具读出 5 级都会出 ✓（沙发的 CG 就是探测器那条带出来的 ✓）。
            // `UIManager.PlayCg` 现在还是占位日志 ✓ —— 美术的 CG 到了直接填 ✓，这里不用再动 ✓。
            if (reading.Kind == CaseReadingKind.Emf && reading.Emf >= 5 && interactable != null
                && Services.TryGet<UIManager>(out var cgUi))
            {
                cgUi.PlayCg($"{interactable.InteractableId}_emf5");
            }

            if (!string.IsNullOrWhiteSpace(reading.Flag) && Services.TryGet<FlagManager>(out var flagManager))
            {
                flagManager.Set(reading.Flag);
            }

            // **每条读数都报给案情** ✓ —— 一条家具读了几条、其中几条异常 ✓，判定权重迟早要用这个账本 ✓。
            NotifyCaseDirector(reading);

            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.ShowToolResult(
                    string.IsNullOrWhiteSpace(reading.Text) ? "读数已记录。" : reading.Text,
                    true);
            }

            // 第一条读数 = "查过这里" ✓ → 收集 / 证据 / 标已查 / 解锁拾取 ✓，**只做一次** ✓。
            if (!unlockedByReading)
            {
                unlockedByReading = true;
                return ApplyPrimaryCollection();
            }

            return true;
        }

        /// <summary>
        /// "查过这里"的收尾 ✓（**只做一次** ✓）：收集 ✓ → 证据 ✓ → 标已查 / 失效 ✓ → 解锁拾取 ✓。
        /// 报案情**不在这里** ✗ —— 每条读数都要报 ✓（那是"多份读数"的账本 ✓），见 <see cref="ResolveReading"/> ✓。
        /// </summary>
        private bool ApplyPrimaryCollection()
        {
            if (!ApplyCollection())
            {
                Fail();
                return false;
            }

            ApplyEvidence();

            if (markCollectedOnSuccess)
            {
                interactable.SetCollected();
            }
            else if (deactivateOnSuccess)
            {
                interactable.SetActive(false);
            }

            // 读过一次 → 把本局的收容物交到手上 ✓（在那之前详情区是空的 ✓，「拾取」自然不可用 ✓）。
            RevealPendingClue();
            return true;
        }

        /// <summary>按条目登记的观测种类写进 <see cref="BranchManager"/> ✓（副读数也照样登记 ✓）。</summary>
        private void RegisterObservation(ToolReading reading)
        {
            if (interactable == null || !Services.TryGet<BranchManager>(out var branchManager))
            {
                return;
            }

            var interactableId = interactable.InteractableId;
            switch (reading.Kind)
            {
                case CaseReadingKind.Temperature:
                    branchManager.RegisterTemperature(interactableId, reading.Temperature);
                    break;
                case CaseReadingKind.Emf:
                    branchManager.RegisterEMF(interactableId, reading.Emf);
                    break;
                case CaseReadingKind.Uv:
                    branchManager.RegisterUVResult(interactableId, reading.Uv);
                    break;
                case CaseReadingKind.Audio:
                    branchManager.RegisterAudioResult(interactableId, reading.Audio);
                    break;
            }
        }

        /// <summary>老数据只登记了观测值 ✓、没有"这条异常不异常"的标记 ✗ —— 照 §2 两张表的口径反推 ✓。</summary>
        private const float AnomalyTemperatureThreshold = 10f;

        /// <summary>
        /// 只给老数据用 ✓：EMF ≥ 5 ✓、温度明显偏低 ✓、录到嘶吼 / 嗡鸣 / 低语 ✓、紫外线有残留 ✓。
        /// 案情摇出来的读数**不用这个** ✗ —— 它们自己带着 <see cref="ToolReading.IsAnomaly"/> ✓。
        /// </summary>
        private static bool IsAnomalousReading(
            CaseReadingKind kind, float temperature, int emf, bool uv, AudioType audio)
        {
            switch (kind)
            {
                case CaseReadingKind.Temperature:
                    return temperature <= AnomalyTemperatureThreshold;
                case CaseReadingKind.Emf:
                    return emf >= 5;
                case CaseReadingKind.Uv:
                    return uv;
                case CaseReadingKind.Audio:
                    return audio == AudioType.Scream || audio == AudioType.Hum || audio == AudioType.Whisper;
                default:
                    return false;
            }
        }

        private void Awake()
        {
            interactable = GetComponent<SimpleInteractable>();
        }

        public bool TryResolveFromClick()
        {
            if (!resolveOnClick)
            {
                return false;
            }

            return Resolve(null);
        }

        public bool TryResolveWithTool(ToolItem toolItem)
        {
            return Resolve(toolItem);
        }

        private bool Resolve(ToolItem toolItem)
        {
            if (interactable == null || !interactable.IsActive)
            {
                return false;
            }

            // 门槛：没先检视就上工具 —— 只给提示，**不扣 SAN、不算用错工具、也不消耗耐久**
            // （这是流程要求，不是玩家的错；点一下把它放大看清，再来就能用了）。
            if (requiresInspection && toolItem != null && !IsInspected)
            {
                if (Services.TryGet<UIManager>(out var hintUi))
                {
                    hintUi.ShowHint(inspectionRequiredText, 2f);
                }

                return false;
            }

            // **先查读数条目** ✓（"一件家具多把工具各给一条读数"的基础 ✓）：
            // 命中就走条目那条路 ✓ —— **每条读数一律平等** ✓（异常与否由读数自己带着 ✓，见 ToolReading.IsAnomaly ✓）。
            // 场景里烘好的老数据留空 ✓ → EnsureReadings 会用 requiredToolType / secondaryToolType 拼出条目 ✓。
            var reading = FindReading(toolItem != null ? toolItem.ToolType : ToolType.None);
            if (reading.HasValue)
            {
                return ResolveReading(reading.Value, toolItem);
            }

            // **工具不到位也允许用** ✓ —— 只是读不出东西 ✓（恐鬼症那套：不对口 ≠ 不能碰 ✗）。
            if (toolItem != null && requiredToolType != ToolType.None && toolItem.ToolType != requiredToolType)
            {
                return ResolveOffTopic(toolItem);
            }

            if (requiredToolType != ToolType.None)
            {
                if (toolItem == null || !toolItem.Use())
                {
                    Fail();
                    return false;
                }
            }

            RegisterObservations();
            if (!ApplyCollection())
            {
                Fail();
                return false;
            }
            ApplyEvidence();
            ApplyFlags();
            ShowSuccess();
            NotifyCaseDirector();

            // 探测器读到 **5 级** = 检测异常 ✓ → 要出 CG（见 Docs/ContainmentRules.md §2.1 ✓）。
            // 现在 `UIManager.PlayCg` 是占位日志 ✓ —— 美术的 CG 一到就填进去 ✓，这里不用再动 ✓。
            if (registerEmf && emfValue >= 5 && interactable != null
                && Services.TryGet<UIManager>(out var cgUi))
            {
                cgUi.PlayCg($"{interactable.InteractableId}_emf5");
            }

            if (markCollectedOnSuccess)
            {
                interactable.SetCollected();
            }
            else if (deactivateOnSuccess)
            {
                interactable.SetActive(false);
            }

            // 读过一次 → 把本局的收容物交到手上 ✓（在那之前详情区是空的 ✓，「拾取」自然不可用 ✓）。
            // **不重开面板** ✗→✓：拾取靠"选中这件家具 + 上拖" ✓（见 ItemActionRunner.Pickup 里的兜底 ✓），
            // 不需要详情区额外弹一次 ✗ —— 面板盖着房间反而碍事 ✓。
            RevealPendingClue();

            return true;
        }

        /// <summary>
        /// 把"这里被读了一条读数"报给案情 ✓（通用关卡器里，证据和判定都由 CaseDirector 管 ✓）。
        /// <paramref name="reading"/> 为空 = 老路径读的 ✓（不知道异常与否 ✓）。
        /// </summary>
        private void NotifyCaseDirector(ToolReading? reading = null)
        {
            if (interactable == null || !Services.TryGet<CaseDirector>(out var caseDirector))
            {
                return;
            }

            caseDirector.NotifyRead(
                interactable.InteractableId,
                reading?.IsAnomaly ?? false,
                DescribeEffect(reading),
                reading?.Text);
        }

        /// <summary>
        /// 读数来源的工具名 ✓（日志用 ✓：一眼看清"这件家具哪几把工具读过什么"✓）。
        /// 优先用读数自带的显示名 ✓（来自读数种类表 ✓）；老数据没有才按工具类型猜 ✓。
        /// </summary>
        private static string DescribeEffect(ToolReading? reading)
        {
            if (reading == null)
            {
                return null;
            }

            return string.IsNullOrEmpty(reading.Value.ToolDisplayName)
                ? DescribeTool(reading.Value.Tool)
                : reading.Value.ToolDisplayName;
        }

        /// <summary>
        /// 老数据兜底 ✓：按工具类型给个中文名 ✓。
        /// ⚠ **别在这里加新种类** ✗ —— 新读数请去读数种类表的 `DisplayName` 那一栏填 ✓。
        /// </summary>
        private static string DescribeTool(ToolType toolType)
        {
            switch (toolType)
            {
                case ToolType.Thermometer: return "温度枪";
                case ToolType.Detector: return "探测器";
                case ToolType.Recorder: return "录音笔";
                case ToolType.UVLight: return "紫外线灯";
                case ToolType.ToolKit: return "工具包";
                default: return "空手";
            }
        }

        private void RegisterObservations()
        {
            if (!Services.TryGet<BranchManager>(out var branchManager) || interactable == null)
            {
                return;
            }

            var interactableId = interactable.InteractableId;
            if (registerTemperature)
            {
                branchManager.RegisterTemperature(interactableId, temperatureValue);
            }

            if (registerEmf)
            {
                branchManager.RegisterEMF(interactableId, emfValue);
            }

            if (registerUv)
            {
                branchManager.RegisterUVResult(interactableId, uvValue);
            }

            if (registerAudio)
            {
                branchManager.RegisterAudioResult(interactableId, audioType);
            }
        }

        private bool ApplyCollection()
        {
            if (clueItem == null || !Services.TryGet<InventoryManager>(out var inventoryManager))
            {
                return true;
            }

            if (collectToContainment)
            {
                return inventoryManager.AddToContainment(clueItem);
            }

            return inventoryManager.AddToInventory(clueItem);
        }

        private void ApplyEvidence()
        {
            if (!Services.TryGet<EvidenceManager>(out var evidenceManager))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(evidenceId))
            {
                evidenceManager.AddEvidence(evidenceId);
            }
            else if (clueItem != null)
            {
                evidenceManager.OnItemCollected(clueItem);
            }
        }

        private void ApplyFlags()
        {
            if (string.IsNullOrWhiteSpace(successFlag) || !Services.TryGet<FlagManager>(out var flagManager))
            {
                return;
            }

            flagManager.Set(successFlag);
        }

        private void ShowSuccess()
        {
            if (!Services.TryGet<UIManager>(out var uiManager))
            {
                return;
            }

            uiManager.ShowToolResult(successText, collectToContainment);
            uiManager.HideInspector();
        }

        private void Fail()
        {
            if (Services.TryGet<SanityManager>(out var sanityManager) && failureSanityPenalty > 0)
            {
                sanityManager.ReduceSanity(failureSanityPenalty);
            }

            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.ShowHint(failureText, 2f);
                uiManager.ShowToolResult(failureText, true);
            }
        }
    }
}
