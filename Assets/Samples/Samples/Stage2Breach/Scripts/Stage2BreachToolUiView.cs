using System.Collections.Generic;
using Project.Core.Runtime.Framework;
using Project.Gameplay.Scripts.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Samples.Stage2Breach.Scripts
{
    public sealed class Stage2BreachToolUiView : MonoBehaviour
    {
        [SerializeField] private GameObject toolbarPanel;
        [SerializeField] private Button[] toolButtons;
        [SerializeField] private Text[] toolButtonTexts;
        [SerializeField] private Image[] toolSelectionHighlights;
        [SerializeField] private Image toolDragIndicator;

        private Color defaultToolDragColor;
        private bool hasToolDragColor;
        private Camera uiCamera;

        public void Initialize()
        {
            BindToolButtons();
            if (toolDragIndicator != null)
            {
                var rootCanvas = toolDragIndicator.canvas;
                uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? rootCanvas.worldCamera
                    : null;

                defaultToolDragColor = toolDragIndicator.color;
                hasToolDragColor = true;
                toolDragIndicator.enabled = false;
                toolDragIndicator.raycastTarget = false;
                toolDragIndicator.sprite = null;
                toolDragIndicator.preserveAspect = false;

                var rect = toolDragIndicator.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(96f, 96f);
            }
        }

        public void ShowToolDrag(Sprite sprite, Vector2 screenPosition)
        {
            if (toolDragIndicator == null)
            {
                return;
            }

            var rect = toolDragIndicator.rectTransform;
            toolDragIndicator.transform.SetAsLastSibling();
            toolDragIndicator.sprite = sprite;
            toolDragIndicator.enabled = sprite != null;
            rect.sizeDelta = new Vector2(96f, 96f);

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
            var canvasRect = toolDragIndicator.canvas != null
                ? toolDragIndicator.canvas.transform as RectTransform
                : null;

            if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out var canvasLocalPoint))
            {
                rect.anchoredPosition = canvasLocalPoint;
                return;
            }

            rect.position = screenPosition;
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

        public void SetToolbar(IReadOnlyList<ToolItem> tools, int selectedSlot)
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
                var tool = tools != null && i < tools.Count ? tools[i] : null;
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
                    var toolInput = GetToolInput();
                    toolInput?.SelectSlot(slotIndex);
                });
            }
        }

        private IToolInputService GetToolInput()
        {
            return Services.TryGet<IToolInputService>(out var service) ? service : null;
        }
    }
}
