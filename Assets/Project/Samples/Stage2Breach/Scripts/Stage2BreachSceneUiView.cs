using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;
using Project.Narrative.Scripts;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Samples.Stage2Breach.Scripts
{
    public sealed class Stage2BreachSceneUiView : MonoBehaviour
    {
        [SerializeField] private GameObject vnPanel;
        [SerializeField] private Text vnSpeakerText;
        [SerializeField] private Text vnBodyText;
        [SerializeField] private Text vnContinueHintText;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private Text[] choiceButtonTexts;

        [SerializeField] private GameObject toolbarPanel;
        [SerializeField] private Button[] toolButtons;
        [SerializeField] private Text[] toolButtonTexts;
        [SerializeField] private Image[] toolSelectionHighlights;

        [SerializeField] private Text sanityText;
        [SerializeField] private Text evidenceText;
        [SerializeField] private Text containmentText;
        [SerializeField] private Text hintText;
        [SerializeField] private Text resultText;
        [SerializeField] private Image toolDragIndicator;

        [SerializeField] private GameObject inspectorPanel;
        [SerializeField] private Text inspectorTitleText;
        [SerializeField] private Text inspectorBodyText;

        private readonly List<string> currentChoiceIds = new();
        private Stage2BreachToolInput toolInput;
        private Color defaultResultColor;
        private bool hasResultColor;
        private Color defaultToolDragColor;
        private bool hasToolDragColor;

        private void Awake()
        {
            toolInput = FindFirstObjectByType<Stage2BreachToolInput>();
            BindToolButtons();
            HideChoices();
            SetVnVisible(false);
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

            if (toolDragIndicator != null)
            {
                defaultToolDragColor = toolDragIndicator.color;
                hasToolDragColor = true;
                toolDragIndicator.enabled = false;
                toolDragIndicator.raycastTarget = false;
            }
        }

        private void Update()
        {
            if (vnPanel == null || !vnPanel.activeSelf || currentChoiceIds.Count > 0)
            {
                return;
            }

            if ((Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Space)) && Services.TryGet<VNDirector>(out var vnDirector))
            {
                vnDirector.Advance().Forget();
            }
        }

        public void SetVnVisible(bool visible)
        {
            if (vnPanel != null)
            {
                vnPanel.SetActive(visible);
            }
        }

        public void SetVnLine(string speakerName, string text)
        {
            SetVnVisible(true);
            if (vnSpeakerText != null)
            {
                vnSpeakerText.text = string.IsNullOrWhiteSpace(speakerName) ? "旁白" : speakerName;
            }

            if (vnBodyText != null)
            {
                vnBodyText.text = string.IsNullOrWhiteSpace(text) ? string.Empty : text;
            }

            UpdateContinueHint();
        }

        public void SetChoices(IReadOnlyList<VNChoiceViewData> choices)
        {
            currentChoiceIds.Clear();
            for (var i = 0; i < choiceButtons.Length; i++)
            {
                var button = choiceButtons[i];
                if (button != null)
                {
                    button.gameObject.SetActive(false);
                }
            }

            if (choices != null)
            {
                var count = Mathf.Min(choices.Count, choiceButtons.Length);
                for (var i = 0; i < count; i++)
                {
                    var choice = choices[i];
                    currentChoiceIds.Add(choice?.ChoiceId ?? string.Empty);
                    if (choiceButtons[i] != null)
                    {
                        choiceButtons[i].gameObject.SetActive(choice != null);
                    }

                    if (choiceButtonTexts != null && i < choiceButtonTexts.Length && choiceButtonTexts[i] != null)
                    {
                        choiceButtonTexts[i].text = choice?.Text ?? string.Empty;
                    }
                }
            }

            UpdateContinueHint();
        }

        public void HideChoices()
        {
            currentChoiceIds.Clear();
            if (choiceButtons == null)
            {
                return;
            }

            foreach (var button in choiceButtons)
            {
                if (button != null)
                {
                    button.gameObject.SetActive(false);
                }
            }

            UpdateContinueHint();
        }

        public void SetHint(string content, float duration)
        {
            if (hintText == null)
            {
                return;
            }

            hintText.text = content ?? string.Empty;
            ClearHintAfterDelay(duration).Forget();
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

        public void ShowToolDrag(Sprite sprite, Vector2 screenPosition)
        {
            if (toolDragIndicator == null)
            {
                return;
            }

            toolDragIndicator.sprite = sprite;
            toolDragIndicator.enabled = true;
            SetToolDragValidity(true);
            UpdateToolDrag(screenPosition);
        }

        public void UpdateToolDrag(Vector2 screenPosition)
        {
            if (toolDragIndicator == null)
            {
                return;
            }

            var rect = toolDragIndicator.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = screenPosition;
        }

        public void HideToolDrag()
        {
            if (toolDragIndicator == null)
            {
                return;
            }

            toolDragIndicator.enabled = false;
            toolDragIndicator.sprite = null;
            if (hasToolDragColor)
            {
                toolDragIndicator.color = defaultToolDragColor;
            }
        }

        public void SetToolDragValidity(bool isValid)
        {
            if (toolDragIndicator == null || !hasToolDragColor)
            {
                return;
            }

            toolDragIndicator.color = isValid ? defaultToolDragColor : new Color(1f, 0.45f, 0.45f, defaultToolDragColor.a);
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

        public void SetToolbar(ToolItem[] tools, int selectedSlot)
        {
            if (toolbarPanel != null)
            {
                toolbarPanel.SetActive(true);
            }

            if (toolButtons == null || toolButtonTexts == null || toolSelectionHighlights == null)
            {
                return;
            }

            for (var i = 0; i < toolButtons.Length; i++)
            {
                var tool = tools != null && i < tools.Length ? tools[i] : null;
                if (toolButtonTexts.Length > i && toolButtonTexts[i] != null)
                {
                    toolButtonTexts[i].text = tool != null
                        ? $"{i + 1}. {tool.DisplayName}\n{tool.ToolType} {tool.Durability}/{tool.MaxDurability}"
                        : $"{i + 1}. 空";
                }

                if (toolSelectionHighlights.Length > i && toolSelectionHighlights[i] != null)
                {
                    toolSelectionHighlights[i].enabled = i == selectedSlot;
                }

                if (toolButtons.Length > i && toolButtons[i] != null)
                {
                    toolButtons[i].interactable = tool != null;
                }
            }
        }

        public void ShowInspector(Item item, SimpleInteractable interactable)
        {
            SetInspectorVisible(true);
            if (inspectorTitleText != null)
            {
                inspectorTitleText.text = item != null && !string.IsNullOrWhiteSpace(item.DisplayName)
                    ? item.DisplayName
                    : interactable != null ? interactable.InteractableId : "调查";
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

        private void BindToolButtons()
        {
            if (toolButtons == null)
            {
                return;
            }

            for (var i = 0; i < toolButtons.Length; i++)
            {
                var slotIndex = i;
                var button = toolButtons[i];
                if (button == null)
                {
                    continue;
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    EnsureToolInput();
                    toolInput?.SelectSlot(slotIndex);
                });
            }
        }

        private void EnsureToolInput()
        {
            if (toolInput == null)
            {
                toolInput = FindFirstObjectByType<Stage2BreachToolInput>();
            }
        }

        private void UpdateContinueHint()
        {
            if (vnContinueHintText != null)
            {
                vnContinueHintText.text = currentChoiceIds.Count > 0 ? string.Empty : "空格 / 右键继续";
            }
        }

        private void SetInspectorVisible(bool visible)
        {
            if (inspectorPanel != null)
            {
                inspectorPanel.SetActive(visible);
            }
        }

        private async UniTaskVoid ClearHintAfterDelay(float duration)
        {
            if (hintText == null || duration <= 0f)
            {
                return;
            }

            await UniTask.Delay((int)(duration * 1000f));
            if (hintText != null)
            {
                hintText.text = string.Empty;
            }
        }
    }
}
