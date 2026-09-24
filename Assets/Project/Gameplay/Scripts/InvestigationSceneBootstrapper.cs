using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Case;
using Project.Gameplay.Scripts.Items;
using Project.Gameplay.Scripts.Tools;
using UnityEngine;

namespace Project.Gameplay.Scripts
{
    /// <summary>
    /// 探索场景（别墅客厅）的启动流程：整个玩法场景只有这一个"组装点"。
    ///
    /// 顺序按设计规范 §8「关卡运行流程」：Init → StartLevel → 库存/装备 → SAN → 证据 → 分支 → flag
    /// → 刷新 HUD → Exploration → 环境音。（交互物的注册由各自身上的 SimpleInteractableAutoRegister 负责。）
    /// </summary>
    public sealed class InvestigationSceneBootstrapper : MonoBehaviour
    {
        [Header("关卡")]
        [SerializeField] private string levelId = "px2050_villa_living_room";

        [Header("初始装备（会同时装备到 InventoryManager 的工具槽）")]
        [SerializeField] private ToolItem[] initialTools = new ToolItem[0];
        [Tooltip("场景里所有可能出现的 Item：读档时要按 itemId 反解成引用，所以要把它们都登记进去。")]
        [SerializeField] private Item[] knownItems = new Item[0];

        [Header("初始数值")]
        [SerializeField] private int initialSanity = 4;
        [SerializeField] private int evidenceGoal = 3;
        [SerializeField] private int branchSeed = 2050;

        [Header("本局案情（通用关卡器：身份/读数/工具按种子随机）")]
        [Tooltip("留空就会在自己身上找 CaseDirector；再找不到 = 这一关不做随机案情（老式写死内容）。")]
        [SerializeField] private CaseDirector caseDirector;

        [Header("初始 flag")]
        [SerializeField] private string[] initialTrueFlags = { "px2050_villa_started", "location_whitewan_villa" };

        [Tooltip("证据读数凑够时写的 flag（够 = 可以下结论了；不写死关卡名，通用器里换个名字就行）。")]
        [SerializeField] private string evidenceReadyFlag = "case_evidence_ready";

        [Header("环境音")]
        [SerializeField] private string ambienceId = "villa_night_ambience";
        [SerializeField] private float ambienceFadeDuration = 2f;

        [Header("调试")]
        [SerializeField] private bool logBootstrap = true;

        private bool subscribed;

        private async void Start()
        {
            // 等一帧：所有 ManagerBehaviour.Awake 都会在场景加载时注册进 Services，这里保证拿得到。
            await UniTask.Yield();
            await BootstrapAsync();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private async UniTask BootstrapAsync()
        {
            // 通用关卡器：案情组件就在自己身上（建造工具挂在同一根节点），省得在场景里连引用。
            if (caseDirector == null)
            {
                caseDirector = GetComponent<CaseDirector>();
            }

            if (!Services.TryGet<GameManager>(out var gameManager)
                || !Services.TryGet<GameLoopManager>(out var gameLoopManager)
                || !Services.TryGet<InventoryManager>(out var inventoryManager)
                || !Services.TryGet<SanityManager>(out var sanityManager)
                || !Services.TryGet<EvidenceManager>(out var evidenceManager)
                || !Services.TryGet<BranchManager>(out var branchManager))
            {
                Debug.LogError("[Villa] 场景缺少必要的 Manager（GameManager/GameLoopManager/InventoryManager/SanityManager/EvidenceManager/BranchManager），玩法无法启动。");
                return;
            }

            gameManager.SwitchState(GameState.Init);
            gameLoopManager.StartLevel(levelId);

            inventoryManager.SetItemSource(knownItems);
            await inventoryManager.Initialize();
            ApplyLoadout(inventoryManager, initialTools);

            // 本局种子只在这里决定一次：BranchManager 播种和案情摇号用同一颗。
            // 案情自己用 System.Random 算，不碰 Unity 全局随机，所以两边互不干扰、也跟调用顺序无关。
            var caseSeed = caseDirector != null ? caseDirector.ResolveSeed() : branchSeed;

            await sanityManager.Initialize(initialSanity);
            evidenceManager.Initialize(evidenceGoal);
            branchManager.Initialize(caseSeed);

            SetInitialFlags();
            Subscribe(sanityManager, evidenceManager);

            // 案情要在工具装备好、初始 flag 设好之后摇：它会把这一局的读数灌进每件家具的规则里。
            // （它还会把证据目标改成"要凑够几条异常"，所以 evidenceGoal 只是没有案情组件时的兜底值。）
            caseDirector?.BeginCase(caseSeed);

            // 本局带哪几件由案情决定（工具池里随机少带一件）——
            // 装备位和工具条都要跟着换，否则会出现"HUD 显示新一套、拖出来却是旧一套"。
            ApplyLoadout(inventoryManager,
                caseDirector != null && caseDirector.EquippedTools.Count > 0 ? caseDirector.EquippedTools : initialTools);

            RefreshHud();

            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.PlaySceneTransition("crt");
            }

            gameManager.SwitchState(GameState.Exploration);

            if (Services.TryGet<AudioManager>(out var audioManager))
            {
                audioManager.PlayAmbience(ambienceId, ambienceFadeDuration);
            }

            if (logBootstrap)
            {
                Debug.Log($"[Villa] 探索场景已就绪：关卡 {levelId}，SAN {initialSanity}，工具 {initialTools.Length} 件。"
                          + (caseDirector != null
                              ? $"本局案情：种子 {caseDirector.Seed}，身份 {(caseDirector.IsFakeHuman ? "伪人" : "正常人")}，"
                                + $"要凑 {caseDirector.CorroborationNeeded} 条异常读数。"
                              : $"证据目标 {evidenceGoal}。"));
            }
        }

        private void ApplyLoadout(InventoryManager inventoryManager, IReadOnlyList<ToolItem> loadout)
        {
            if (inventoryManager == null || loadout == null)
            {
                return;
            }

            for (var i = 0; i < loadout.Count && i < inventoryManager.EquipmentCapacity; i++)
            {
                var tool = loadout[i];
                if (tool == null)
                {
                    continue;
                }

                // 每次进入关卡都把耐久恢复满（耐久本身会被存档）。
                tool.RestoreDurability();
                inventoryManager.EquipTool(tool, i);
            }

            // 工具条也要换成同一套：ToolBeltInput 的槽位/工具表原本是按固定 4 件接的。
            var belt = GetComponent<ToolBeltInput>();
            if (belt != null)
            {
                belt.SetTools(loadout);
            }
        }

        private void SetInitialFlags()
        {
            if (!Services.TryGet<FlagManager>(out var flagManager) || initialTrueFlags == null)
            {
                return;
            }

            foreach (var flag in initialTrueFlags)
            {
                if (!string.IsNullOrWhiteSpace(flag))
                {
                    flagManager.Set(flag);
                }
            }
        }

        private void Subscribe(SanityManager sanityManager, EvidenceManager evidenceManager)
        {
            if (subscribed)
            {
                return;
            }

            sanityManager.OnSanityDepleted += HandleSanityDepleted;
            evidenceManager.OnGoalReached += HandleGoalReached;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (Services.TryGet<SanityManager>(out var sanityManager))
            {
                sanityManager.OnSanityDepleted -= HandleSanityDepleted;
            }

            if (Services.TryGet<EvidenceManager>(out var evidenceManager))
            {
                evidenceManager.OnGoalReached -= HandleGoalReached;
            }

            subscribed = false;
        }

        private void HandleSanityDepleted()
        {
            // SAN 归零目前没有专门的结算界面，至少给玩家一个明确的反馈，别让画面就那么卡住。
            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.ShowHint("精神已经撑不住了……这次的调查到此为止。", 5f);
                uiManager.ShowToolResult("SAN 归零：调查失败。", true);
            }

            Debug.Log("[Villa] SAN 归零，进入 GameOver。");

            if (Services.TryGet<GameManager>(out var gameManager))
            {
                gameManager.TriggerGameOver().Forget();
            }
        }

        private void HandleGoalReached()
        {
            // 证据读数凑够 = 异常条数够互相印证 = 可以下结论了
            // （案情会把证据目标设成和判定门槛同一个值，所以这条提示就是"该收尾了"的信号）。
            if (Services.TryGet<FlagManager>(out var flagManager) && !string.IsNullOrWhiteSpace(evidenceReadyFlag))
            {
                flagManager.Set(evidenceReadyFlag);
            }

            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.ShowHint("异常读数已经够互相印证了 —— 回详情区下结论。", 4f);
            }
        }

        private void RefreshHud()
        {
            if (!Services.TryGet<UIManager>(out var uiManager))
            {
                return;
            }

            var currentSanity = 0;
            var maxSanity = 0;
            if (Services.TryGet<SanityManager>(out var sanityManager))
            {
                currentSanity = sanityManager.CurrentSanity;
                maxSanity = sanityManager.MaxSanity;
            }

            var containmentCapacity = Services.TryGet<InventoryManager>(out var inventoryManager)
                ? inventoryManager.ContainmentCapacity
                : 0;

            uiManager.UpdateSanDisplay(currentSanity, maxSanity);
            uiManager.UpdateEquipmentSlots();
            uiManager.UpdateContainmentDisplay(0, containmentCapacity);
        }
    }
}
