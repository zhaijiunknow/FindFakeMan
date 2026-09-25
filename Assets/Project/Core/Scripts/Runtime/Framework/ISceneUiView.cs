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

        /// <summary>
        /// **取消选中** ✓（右键 / 点空白处走它 ✓）：清掉当前目标 + 熄灭常驻描边 + 收起详情区 ✓。
        /// 和 <see cref="HideInspector"/> 的区别 ✗：那个只是"收起面板"✓，选中的目标还留着 ✓。
        /// </summary>
        void ClearSelection();
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
