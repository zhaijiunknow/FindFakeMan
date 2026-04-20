using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
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

        private SimpleInteractable interactable;

        public bool ResolveOnClick => resolveOnClick;

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

            if (requiredToolType != ToolType.None)
            {
                if (toolItem == null || toolItem.ToolType != requiredToolType || !toolItem.Use())
                {
                    Fail();
                    return false;
                }
            }

            RegisterObservations();
            ApplyCollection();
            ApplyEvidence();
            ApplyFlags();
            ShowSuccess();

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

        private void ApplyCollection()
        {
            if (clueItem == null || !Services.TryGet<InventoryManager>(out var inventoryManager))
            {
                return;
            }

            if (collectToContainment)
            {
                inventoryManager.AddToContainment(clueItem);
                return;
            }

            inventoryManager.AddToInventory(clueItem);
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
