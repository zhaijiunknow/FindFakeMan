using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Gameplay.Scripts.Interactables
{
    /// <summary>
    /// UI 版交互物点击/悬停。
    ///
    /// 为什么不直接用 <see cref="SimpleInteractableClickHandler"/>：它走 `OnMouseDown` + `[RequireComponent(typeof(Collider))]`，
    /// 而且指针在 UI 上时会 `IsPointerOverGameObject()` 直接 return。本项目的玩法**全部在 UI 里**
    /// （房间图就是 Canvas 上的 Image），所以需要 uGUI 的 Pointer 接口版本。
    ///
    /// 流程：点一下 → 若正在拖工具则交给工具，否则 <see cref="InteractionManager.OnInteractableClicked"/>（进 Inspection 并开检视面板）。
    /// </summary>
    [RequireComponent(typeof(SimpleInteractable))]
    public sealed class SimpleInteractablePointerHandler : MonoBehaviour,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [Header("引用")]
        [Tooltip("悬停高亮的目标图层（现在交互物自己就是图层，直接填自己）。")]
        [SerializeField] private Image highlightTarget;

        [Header("命中")]
        [Tooltip("按贴图 alpha 判定命中：只在 alpha 超过它的像素上算悬停/点击（0 = 关掉，整帧矩形都算命中）。\n"
                 + "家具图是整帧 3840×2160 的，不开这个就会出现「鼠标在桌子外面也触发桌子」。\n"
                 + "要求贴图 Read/Write（Tools/Project/Gameplay/Fix Living Room Sprite Import 会保证）。")]
        [SerializeField, Range(0f, 1f)] private float alphaHitTestThreshold = 0.5f;

        [Header("悬停表现")]
        [Tooltip("平常颜色（一般就是白色，不改动美术）。")]
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("悬停颜色：偏暖一点点，作为「这里可以点」的提示。")]
        [SerializeField] private Color hoverColor = new Color(1f, 0.97f, 0.85f, 1f);

        private SimpleInteractable interactable;

        private void Awake()
        {
            interactable = GetComponent<SimpleInteractable>();

            // 运行时兜底：场景里那个 Image 的 alpha 阈值如果还是 0（默认值不写进 YAML，很容易漏），
            // 就在这里补上 —— 否则悬停/点击会按整帧矩形判定，"鼠标在桌子外面也能选中桌子"。
            if (highlightTarget == null)
            {
                highlightTarget = GetComponent<Image>();
            }

            if (highlightTarget != null && alphaHitTestThreshold > 0f
                && highlightTarget.alphaHitTestMinimumThreshold <= 0f)
            {
                highlightTarget.alphaHitTestMinimumThreshold = alphaHitTestThreshold;
                Debug.Log($"[Pointer] {name} 补上 alpha 命中阈值 {alphaHitTestThreshold:0.##}"
                          + "（只在不透明像素上算悬停/点击）。");
            }
        }

        private void OnDisable()
        {
            ApplyHighlight(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ApplyHighlight(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ApplyHighlight(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (interactable == null || !interactable.IsActive)
            {
                return;
            }

            // 正在拖工具时，"点一下"不是点击，交给工具拖拽流程（松手时由 ToolBeltInput 判定落点）。
            if (Services.TryGet<IToolInputService>(out var toolInput) && toolInput.IsDragging)
            {
                return;
            }

            if (Services.TryGet<InteractionManager>(out var interactionManager))
            {
                interactionManager.OnInteractableClicked(interactable);
            }
        }

        private void ApplyHighlight(bool on)
        {
            if (highlightTarget == null)
            {
                return;
            }

            highlightTarget.color = on ? hoverColor : normalColor;
        }
    }
}
