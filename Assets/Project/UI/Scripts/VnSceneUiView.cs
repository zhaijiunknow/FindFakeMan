using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;
using Project.Narrative.Scripts;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Scripts
{
    /// <summary>
    /// 纯 VN（视觉小说）场景的 UI 视图：实现 ISceneUiView，负责 VN 面板的显示、
    /// 空格/右键推进与选项按钮。游戏性 UI（调查/工具/HUD 等）留空占位。
    /// </summary>
    public sealed class VnSceneUiView : MonoBehaviour, ISceneUiView
    {
        [Header("VN 面板")]
        [SerializeField] private GameObject vnPanel;
        [SerializeField] private Text vnSpeakerText;
        [SerializeField] private Text vnBodyText;
        [SerializeField] private Text vnContinueHintText;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private Text[] choiceButtonTexts;

        private readonly List<string> currentChoiceIds = new();
        private int activeChoiceCount;

        private void Awake()
        {
            Services.Register<ISceneUiView>(this);
            BindChoiceButtons();
            HideChoices();
            SetVnVisible(false);
        }

        private void OnDestroy()
        {
            Services.UnregisterInstance(this);
        }

        private void Update()
        {
            if (vnPanel == null || !vnPanel.activeSelf || activeChoiceCount > 0)
            {
                return;
            }

            // 空格 / 右键推进（与选项按钮冲突最小）
            if ((Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Space))
                && Services.TryGet<VNDirector>(out var vnDirector))
            {
                vnDirector.Advance().Forget();
            }
        }

        // ---- ISceneUiView: VN ----

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
            activeChoiceCount = 0;
            if (choiceButtons != null)
            {
                foreach (var button in choiceButtons)
                {
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
                    var choiceId = choice?.ChoiceId ?? string.Empty;
                    currentChoiceIds.Add(choiceId);

                    var hasChoice = choice != null && !string.IsNullOrWhiteSpace(choiceId);
                    if (choiceButtons[i] != null)
                    {
                        choiceButtons[i].gameObject.SetActive(hasChoice);
                    }

                    if (hasChoice)
                    {
                        activeChoiceCount++;
                    }

                    if (choiceButtonTexts != null && i < choiceButtonTexts.Length && choiceButtonTexts[i] != null)
                    {
                        choiceButtonTexts[i].text = hasChoice ? choice.Text : string.Empty;
                    }
                }
            }

            UpdateContinueHint();
        }

        public void HideChoices()
        {
            currentChoiceIds.Clear();
            activeChoiceCount = 0;
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

        // ---- ISceneUiView: 游戏性 UI（VN 场景用不到，占位） ----

        public void ShowInspector(Item item, SimpleInteractable interactable) { }
        public void HideInspector() { }
        public void ShowToolDrag(Sprite sprite, Vector2 position) { }
        public void UpdateToolDrag(Vector2 position) { }
        public void HideToolDrag() { }
        public void SetToolDragValidity(bool isValid) { }
        public void SetEvidence(int current, int goal) { }
        public void SetSanity(int current, int max) { }
        public void SetContainment(int current, int max) { }
        public void SetToolbar(IReadOnlyList<ToolItem> tools, int selectedSlot) { }
        public void SetHint(string content, float duration) { }
        public void SetResult(string content, bool highlight) { }

        // ---- 选项按钮 ----

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
                vnContinueHintText.text = activeChoiceCount > 0 ? string.Empty : "空格 / 右键继续";
            }
        }
    }
}
