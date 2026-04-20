using System.Collections.Generic;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;
using Project.Narrative.Scripts;
using UnityEngine;

namespace Project.Core.Runtime.Framework
{
    public interface ISceneUiView
    {
        void ShowInspector(Item item, SimpleInteractable interactable);
        void HideInspector();
        void ShowToolDrag(Sprite sprite, Vector2 position);
        void UpdateToolDrag(Vector2 position);
        void HideToolDrag();
        void SetToolDragValidity(bool isValid);
        void SetEvidence(int current, int goal);
        void SetSanity(int current, int max);
        void SetContainment(int current, int max);
        void SetToolbar(IReadOnlyList<ToolItem> tools, int selectedSlot);
        void SetHint(string content, float duration);
        void SetResult(string content, bool highlight);
        void SetVnVisible(bool visible);
        void SetVnLine(string speakerName, string text);
        void SetChoices(IReadOnlyList<VNChoiceViewData> choices);
        void HideChoices();
    }
}
