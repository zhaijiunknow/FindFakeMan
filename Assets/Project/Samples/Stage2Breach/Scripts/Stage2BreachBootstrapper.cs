using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;
using Project.Narrative.Scripts;
using UnityEngine;

namespace Project.Samples.Stage2Breach.Scripts
{
    public sealed class Stage2BreachBootstrapper : MonoBehaviour
    {
        [SerializeField] private string levelId = "stage2_breach";
        [SerializeField] private ToolItem[] initialTools;
        [SerializeField] private VNChapterConfig openingChapter;
        [SerializeField] private int initialSanity = 20;
        [SerializeField] private int evidenceGoal = 3;
        [SerializeField] private int branchSeed = 2050;
        [SerializeField] private string ambienceId = "rain_night_backyard";
        [SerializeField] private float ambienceFadeDuration = 2f;
        [SerializeField] private string[] initialTrueFlags =
        {
            "stage2_breach_started",
            "location_backyard"
        };

        private bool subscribed;

        private async void Start()
        {
            await UniTask.Yield();
            await Bootstrap();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private async UniTask Bootstrap()
        {
            if (!Services.TryGet<GameManager>(out var gameManager) ||
                !Services.TryGet<GameLoopManager>(out var gameLoopManager) ||
                !Services.TryGet<InventoryManager>(out var inventoryManager) ||
                !Services.TryGet<SanityManager>(out var sanityManager) ||
                !Services.TryGet<EvidenceManager>(out var evidenceManager) ||
                !Services.TryGet<BranchManager>(out var branchManager))
            {
                Debug.LogWarning("Stage2BreachBootstrapper could not find all required managers.");
                return;
            }

            Subscribe(sanityManager, evidenceManager);

            gameManager.SwitchState(GameState.Init);
            gameLoopManager.StartLevel(levelId);
            inventoryManager.Initialize().Forget();
            EquipInitialTools(inventoryManager);
            sanityManager.Initialize(initialSanity).Forget();
            evidenceManager.Initialize(evidenceGoal);
            branchManager.Initialize(branchSeed);
            SetInitialFlags();

            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.PlaySceneTransition("fade");
                uiManager.UpdateEquipmentSlots();
            }

            gameManager.SwitchState(GameState.Exploration);

            if (Services.TryGet<AudioManager>(out var audioManager))
            {
                audioManager.PlayAmbience(ambienceId, ambienceFadeDuration);
            }

            await PlayOpeningChapter();
        }

        private void EquipInitialTools(InventoryManager inventoryManager)
        {
            if (initialTools == null)
            {
                return;
            }

            for (var i = 0; i < initialTools.Length; i++)
            {
                var tool = initialTools[i];
                if (tool == null)
                {
                    continue;
                }

                tool.RestoreDurability();
                inventoryManager.EquipTool(tool, i);
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

        private void HandleGoalReached()
        {
            if (Services.TryGet<FlagManager>(out var flagManager))
            {
                flagManager.Set("bedroom_unlocked");
            }

            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.ShowHint("基础证据已收集完成，二楼入口已解锁。", 3f);
            }
        }

        private async UniTask PlayOpeningChapter()
        {
            if (openingChapter == null || !Services.TryGet<VNDirector>(out var vnDirector) || vnDirector.IsPlaying)
            {
                return;
            }

            await vnDirector.StartChapter(openingChapter);
        }

        private void HandleSanityDepleted()
        {
            if (Services.TryGet<GameManager>(out var gameManager))
            {
                gameManager.TriggerGameOver().Forget();
            }
        }
    }
}
