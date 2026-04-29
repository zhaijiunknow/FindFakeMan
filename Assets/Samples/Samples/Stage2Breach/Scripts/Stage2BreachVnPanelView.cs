using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Narrative.Scripts;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Samples.Stage2Breach.Scripts
{
    public sealed class Stage2BreachVnPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject vnPanel;
        [SerializeField] private Text vnSpeakerText;
        [SerializeField] private Text vnBodyText;
        [SerializeField] private Text vnContinueHintText;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private Text[] choiceButtonTexts;

        private readonly List<string> currentChoiceIds = new();

        public void Initialize()
        {
            BindChoiceButtons();
            HideChoices();
            SetVnVisible(false);
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
            if (choiceButtons != null)
            {
                for (var i = 0; i < choiceButtons.Length; i++)
                {
                    var button = choiceButtons[i];
                    if (button != null)
                    {
                        button.gameObject.SetActive(false);
                    }
                }
            }

            if (choices != null && choiceButtons != null)
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

        private void BindChoiceButtons()
        {
            if (choiceButtons == null)
            {
                return;
            }

            for (var i = 0; i < choiceButtons.Length; i++)
            {
                var choiceIndex = i;
                var button = choiceButtons[i];
                if (button == null)
                {
                    continue;
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectChoice(choiceIndex));
            }
        }

        private void SelectChoice(int choiceIndex)
        {
            if (choiceIndex < 0 || choiceIndex >= currentChoiceIds.Count)
            {
                return;
            }

            var choiceId = currentChoiceIds[choiceIndex];
            if (!string.IsNullOrWhiteSpace(choiceId) && Services.TryGet<UIManager>(out var uiManager))
            {
                HideChoices();
                uiManager.SelectVNChoice(choiceId);
            }
        }

        private void UpdateContinueHint()
        {
            if (vnContinueHintText != null)
            {
                vnContinueHintText.text = currentChoiceIds.Count > 0 ? string.Empty : "空格 / 右键继续";
            }
        }
    }
}
