using Cysharp.Threading.Tasks;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Samples.Stage2Breach.Scripts
{
    public sealed class Stage2BreachHudView : MonoBehaviour
    {
        [SerializeField] private Text sanityText;
        [SerializeField] private Text evidenceText;
        [SerializeField] private Text containmentText;
        [SerializeField] private Text hintText;
        [SerializeField] private Text resultText;
        [SerializeField] private GameObject inspectorPanel;
        [SerializeField] private Text inspectorTitleText;
        [SerializeField] private Text inspectorBodyText;

        private Color defaultResultColor;
        private bool hasResultColor;
        private int hintVersion;

        public void Initialize()
        {
            SetInspectorVisible(false);
            if (hintText != null)
            {
                hintText.text = string.Empty;
            }

            if (resultText != null)
            {
                defaultResultColor = resultText.color;
                hasResultColor = true;
                resultText.text = string.Empty;
            }
        }

        public void SetHint(string content, float duration)
        {
            if (hintText == null)
            {
                return;
            }

            hintVersion++;
            hintText.text = content ?? string.Empty;
            ClearHintAfterDelay(hintVersion, duration).Forget();
        }

        public void SetResult(string content, bool highlight)
        {
            if (resultText == null)
            {
                return;
            }

            resultText.text = content ?? string.Empty;
            if (hasResultColor)
            {
                resultText.color = highlight ? new Color(1f, 0.7f, 0.35f) : defaultResultColor;
            }
        }

        public void SetSanity(int current, int max)
        {
            if (sanityText != null)
            {
                sanityText.text = $"SAN {current}/{max}";
            }
        }

        public void SetEvidence(int current, int goal)
        {
            if (evidenceText != null)
            {
                evidenceText.text = $"证据 {current}/{goal}";
            }
        }

        public void SetContainment(int current, int max)
        {
            if (containmentText != null)
            {
                containmentText.text = $"收容 {current}/{max}";
            }
        }

        public void ShowInspector(Item item, SimpleInteractable interactable)
        {
            SetInspectorVisible(true);
            if (inspectorTitleText != null)
            {
                inspectorTitleText.text = item != null && !string.IsNullOrWhiteSpace(item.DisplayName)
                    ? item.DisplayName
                    : interactable != null && !string.IsNullOrWhiteSpace(interactable.InteractableId) ? interactable.InteractableId : "调查";
            }

            if (inspectorBodyText != null)
            {
                inspectorBodyText.text = item != null && !string.IsNullOrWhiteSpace(item.Description)
                    ? item.Description
                    : interactable != null ? interactable.Description : string.Empty;
            }
        }

        public void HideInspector()
        {
            SetInspectorVisible(false);
        }

        private void SetInspectorVisible(bool visible)
        {
            if (inspectorPanel != null)
            {
                inspectorPanel.SetActive(visible);
            }
        }

        private async UniTaskVoid ClearHintAfterDelay(int version, float duration)
        {
            if (hintText == null || duration <= 0f)
            {
                return;
            }

            await UniTask.Delay((int)(duration * 1000f));
            if (hintText != null && version == hintVersion)
            {
                hintText.text = string.Empty;
            }
        }
    }
}
