using Project.Core.Runtime.Framework;
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

            successText = string.IsNullOrEmpty(spec.SuccessText) ? "读数已记录。" : spec.SuccessText;
            failureText = "这把工具在这里读不到东西。";
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

            // 次要工具也算对口（它走观测分支，不参与拾取）。
            if (secondaryToolType != ToolType.None && toolItem.ToolType == secondaryToolType)
            {
                return toolItem.Durability > 0;
            }

            if (requiredToolType == ToolType.None)
            {
                return true;
            }

            return toolItem.ToolType == requiredToolType && toolItem.Durability > 0;
        }

        /// <summary>
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

            // 次要工具走观测分支：登记读数 + flag，不拾取、不进收容、不消耗证据流程。
            if (toolItem != null && secondaryToolType != ToolType.None && toolItem.ToolType == secondaryToolType)
            {
                return ResolveSecondary(toolItem);
            }

            if (requiredToolType != ToolType.None)
            {
                if (toolItem == null || toolItem.ToolType != requiredToolType || !toolItem.Use())
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

            if (markCollectedOnSuccess)
            {
                interactable.SetCollected();
            }
            else if (deactivateOnSuccess)
            {
                interactable.SetActive(false);
            }

            return true;
        }

        /// <summary>把"这里被读过一次"报给案情（通用关卡器里，证据和判定都由 CaseDirector 管）。</summary>
        private void NotifyCaseDirector()
        {
            if (interactable == null || !Services.TryGet<CaseDirector>(out var caseDirector))
            {
                return;
            }

            caseDirector.NotifyRead(interactable.InteractableId);
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
