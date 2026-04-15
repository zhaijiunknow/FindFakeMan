using System;
using UnityEngine;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;

namespace Project.Core.Runtime.Managers
{
    public sealed class UIManager : ManagerBehaviour
    {
        public event Action<string> OnPanelShown;
        public event Action<string> OnPanelHidden;

        public void ShowPanel(string panelId)
        {
            OnPanelShown?.Invoke(panelId);
            Debug.Log($"ShowPanel: {panelId}");
        }

        public void HidePanel(string panelId)
        {
            OnPanelHidden?.Invoke(panelId);
            Debug.Log($"HidePanel: {panelId}");
        }

        public void ShowInspector(Item item, SimpleInteractable interactable)
        {
            Debug.Log($"ShowInspector: {interactable?.InteractableId}");
        }

        public void HideInspector() => Debug.Log("HideInspector");
        public void ShowToolDragIndicator(Sprite sprite, Vector2 position) => Debug.Log($"ShowToolDragIndicator: {position}");
        public void UpdateToolDragIndicator(Vector2 position) => Debug.Log($"UpdateToolDragIndicator: {position}");
        public void HideToolDragIndicator() => Debug.Log("HideToolDragIndicator");
        public void ShowToolValidity(bool isValid) => Debug.Log($"ShowToolValidity: {isValid}");
        public void UpdateEvidenceDisplay(int current, int goal) => Debug.Log($"Evidence: {current}/{goal}");
        public void UpdateSanDisplay(int current, int max) => Debug.Log($"Sanity: {current}/{max}");
        public void UpdateContainmentDisplay(int current, int max) => Debug.Log($"Containment: {current}/{max}");
        public void UpdateEquipmentSlots() => Debug.Log("UpdateEquipmentSlots");
        public void PlaySanDamageEffect(int amount) => Debug.Log($"PlaySanDamageEffect: {amount}");
        public void PlayGlitchEffect(float duration) => Debug.Log($"PlayGlitchEffect: {duration}");
        public void PlayScreenShake(float intensity, float duration) => Debug.Log($"PlayScreenShake: {intensity}, {duration}");
        public void ShowHint(string content, float duration) => Debug.Log($"Hint: {content} ({duration})");
        public void ShowToolHint(string tool, string target, bool valid) => Debug.Log($"ToolHint: {tool} -> {target} ({valid})");
        public void ShowToolResult(string result, bool highlight) => Debug.Log($"ToolResult: {result}, highlight={highlight}");
        public void PlaySceneTransition(string type) => Debug.Log($"SceneTransition: {type}");
    }
}
