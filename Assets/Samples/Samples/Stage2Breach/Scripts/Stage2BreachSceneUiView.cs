using System.Collections.Generic;
using Project.Core.Runtime.Framework;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;
using Project.Narrative.Scripts;
using UnityEngine;

namespace Project.Samples.Stage2Breach.Scripts
{
    public sealed class Stage2BreachSceneUiView : MonoBehaviour, ISceneUiView
    {
        [SerializeField] private Stage2BreachVnPanelView vnPanelView;
        [SerializeField] private Stage2BreachToolUiView toolUiView;
        [SerializeField] private Stage2BreachHudView hudView;

        private void Awake()
        {
            Services.Register<ISceneUiView>(this);
            vnPanelView?.Initialize();
            toolUiView?.Initialize();
            hudView?.Initialize();
        }

        private void OnDestroy()
        {
            Services.UnregisterInstance(this);
        }

        public void SetVnVisible(bool visible)
        {
            vnPanelView?.SetVnVisible(visible);
        }

        public void SetVnLine(string speakerName, string text)
        {
            vnPanelView?.SetVnLine(speakerName, text);
        }

        public void SetChoices(IReadOnlyList<VNChoiceViewData> choices)
        {
            vnPanelView?.SetChoices(choices);
        }

        public void HideChoices()
        {
            vnPanelView?.HideChoices();
        }

        public void SetHint(string content, float duration)
        {
            hudView?.SetHint(content, duration);
        }

        public void SetResult(string content, bool highlight)
        {
            hudView?.SetResult(content, highlight);
        }

        public void ShowToolDrag(Sprite sprite, Vector2 screenPosition)
        {
            toolUiView?.ShowToolDrag(sprite, screenPosition);
        }

        public void UpdateToolDrag(Vector2 screenPosition)
        {
            toolUiView?.UpdateToolDrag(screenPosition);
        }

        public void HideToolDrag()
        {
            toolUiView?.HideToolDrag();
        }

        public void SetToolDragValidity(bool isValid)
        {
            toolUiView?.SetToolDragValidity(isValid);
        }

        public void SetSanity(int current, int max)
        {
            hudView?.SetSanity(current, max);
        }

        public void SetEvidence(int current, int goal)
        {
            hudView?.SetEvidence(current, goal);
        }

        public void SetContainment(int current, int max)
        {
            hudView?.SetContainment(current, max);
        }

        public void SetToolbar(IReadOnlyList<ToolItem> tools, int selectedSlot)
        {
            toolUiView?.SetToolbar(tools, selectedSlot);
        }

        public void ShowInspector(Item item, SimpleInteractable interactable)
        {
            hudView?.ShowInspector(item, interactable);
        }

        public void HideInspector()
        {
            hudView?.HideInspector();
        }
    }
}
