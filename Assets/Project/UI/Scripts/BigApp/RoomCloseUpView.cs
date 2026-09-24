using Project.Core.Runtime.Framework;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Tools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 房间特写切换：正在检视的家具如果带了 <see cref="InteractableCloseUp"/>，
    /// 就把那张整帧特写淡入盖住房间；收起检视（<see cref="InvestigationHudView.CurrentInteractable"/> 变空）再淡出回房间。
    ///
    /// 位置：挂在 RoomView 最上层那个 "CloseUp" 图层上（整帧 Image）。
    /// 特写图和房间图层同尺寸（3840×2160），所以是"切过去"而不是缩放位移。
    ///
    /// 显示时打开 raycastTarget，把底下的家具挡住，于是：
    /// - **点一下特写** = 收起检视（回到房间）；
    /// - 它同时是 <see cref="IInteractableHitProxy"/>：**代表底下那件家具**。
    ///   玩家在特写上松手放工具时，落点解析认的是这件家具而不是坐标 ——
    ///   特写盖住之后，屏幕上的画面和底下那几张透明图的形状已经对不上了，
    ///   不认代理的话"在抽屉正中放探测器"会落在透明处，变成"没有可以调查的东西"。
    ///
    /// 它自己不实现拖拽，所以方向手势照常冒泡到 `game` 上的 InspectorDragHandler：
    /// 在抽屉特写上「上=拾取」就能把记录拿走。
    ///
    /// 为什么用轮询而不是事件：HUD 的当前交互物是被点击流程直接设的，没有"选中变化"事件；
    /// 和 <c>SimpleInteractableVisualState</c> 一样每帧读一次（同时只会有一件家具在特写，开销可忽略）。
    /// </summary>
    public sealed class RoomCloseUpView : MonoBehaviour, IPointerClickHandler, IInteractableHitProxy
    {
        [Header("引用")]
        [Tooltip("场景 HUD：用它知道「现在在检视哪件家具」。")]
        [SerializeField] private InvestigationHudView hud;
        [Tooltip("盖住房间的特写整帧 Image（一般就是自己）。")]
        [SerializeField] private Image closeUpImage;

        [Header("行为")]
        [Tooltip("显示特写时吞掉点击，防止隔着特写点到下面看不见的家具。")]
        [SerializeField] private bool blockClicksWhileShown = true;
        [Tooltip("点一下特写就收起检视面板（回到房间）。")]
        [SerializeField] private bool clickToDismiss = true;
        [Tooltip("检视没配特写的家具时，是否也把特写淡出（正常要开）。")]
        [SerializeField] private bool hideWhenNoCloseUp = true;

        private SimpleInteractable lastInteractable;
        private SimpleInteractable shownTarget;
        private float alpha;
        private float targetAlpha;
        private float fadeSpeed = 5f;
        private float fadeOutDuration = 0.2f;

        /// <summary>特写正显示时，它就代表这件家具（工具拖拽的落点会认它）。</summary>
        public SimpleInteractable HitTarget => alpha > 0.5f ? shownTarget : null;

        private void Awake()
        {
            if (closeUpImage == null)
            {
                closeUpImage = GetComponent<Image>();
            }

            alpha = 0f;
            targetAlpha = 0f;
            Apply();
            RefreshRaycast();
        }

        private void Update()
        {
            // 只有"玩家做了检视（右拖）"才切特写：点一下选中只是开详情区，不该直接放大。
            var current = hud != null && hud.CurrentInteractable != null && hud.CurrentInteractable.IsZoomed
                ? hud.CurrentInteractable
                : null;

            if (current != lastInteractable)
            {
                lastInteractable = current;
                OnSelectionChanged(current);
            }

            if (!Mathf.Approximately(alpha, targetAlpha))
            {
                // unscaled：不管是检视还是暂停，特写的淡入淡出都不该被时间缩放影响。
                alpha = Mathf.MoveTowards(alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
                Apply();
            }

            RefreshRaycast();
        }

        /// <summary>
        /// 每帧决定特写要不要吃射线。
        /// 显示期间一律挡住：特写是整帧不透明图，不挡的话会隔着它点到下面看不见的家具；
        /// 而"在特写上放工具"这件事由 <see cref="HitTarget"/> 兜住，不需要靠射线穿透。
        /// </summary>
        private void RefreshRaycast()
        {
            if (closeUpImage == null)
            {
                return;
            }

            var block = alpha > 0.001f && blockClicksWhileShown;
            if (closeUpImage.raycastTarget != block)
            {
                closeUpImage.raycastTarget = block;
            }
        }

        private void OnSelectionChanged(SimpleInteractable current)
        {
            var config = current != null ? current.GetComponent<InteractableCloseUp>() : null;
            var sprite = config != null ? config.CloseUpSprite : null;

            if (closeUpImage == null)
            {
                return;
            }

            if (sprite != null)
            {
                closeUpImage.sprite = sprite;
                shownTarget = current;
                fadeOutDuration = Mathf.Max(0.01f, config.FadeOutDuration);
                fadeSpeed = 1f / Mathf.Max(0.01f, config.FadeDuration);
                targetAlpha = 1f;
                return;
            }

            if (hideWhenNoCloseUp)
            {
                // 用上一次那件家具的淡出时长，这样"切过去多快、切回来多快"是同一个人配的。
                shownTarget = null;
                fadeSpeed = 1f / fadeOutDuration;
                targetAlpha = 0f;
            }
        }

        private void Apply()
        {
            if (closeUpImage == null)
            {
                return;
            }

            var color = closeUpImage.color;
            closeUpImage.color = new Color(color.r, color.g, color.b, alpha);

            // 全透明时干脆不画：整帧 4K 图的 overdraw 不值得白付。
            // raycastTarget 不在这里管（它有单独的开关），交给 RefreshRaycast。
            closeUpImage.enabled = alpha > 0.001f;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!clickToDismiss || hud == null || alpha < 0.5f)
            {
                return;
            }

            // 方向手势 / 工具拖拽结束时不要顺手把面板关掉。
            if (eventData != null && eventData.dragging)
            {
                return;
            }

            if (Services.TryGet<IToolInputService>(out var toolInput) && toolInput.IsDragging)
            {
                return;
            }

            hud.HideInspector();
        }
    }
}
