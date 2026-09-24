using UnityEngine;

namespace Project.Gameplay.Scripts.Interactables
{
    /// <summary>
    /// 交互物的「特写切换」配置：检视这件家具时，房间视图切到这张整帧特写图
    /// （例如 木桌 → `3b抽屉特写`：抽屉锁着，得先看清里面有什么再决定用什么工具）。
    ///
    /// 这里只放**数据**，切换由 UI 层的 <c>RoomCloseUpView</c> 执行 ——
    /// Gameplay 层不认识 UI（HUD / Image），所以配置留在这边、表现放在那边，
    /// 依赖方向仍然是 UI → Gameplay。
    /// </summary>
    [RequireComponent(typeof(SimpleInteractable))]
    public sealed class InteractableCloseUp : MonoBehaviour
    {
        [Tooltip("特写图：必须是和房间图层同尺寸（3840×2160）的整帧图，才是「切过去」而不是缩放位移。")]
        [SerializeField] private Sprite closeUpSprite;

        [Header("节奏")]
        [Tooltip("切过去的淡入时长（秒）。")]
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.2f;
        [Tooltip("退出特写、切回房间的淡出时长（秒）。")]
        [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.2f;

        /// <summary>特写整帧图（null = 这件家具没有特写，检视时留在房间视图）。</summary>
        public Sprite CloseUpSprite => closeUpSprite;

        /// <summary>淡入时长（秒）。</summary>
        public float FadeDuration => fadeDuration;

        /// <summary>淡出时长（秒）。</summary>
        public float FadeOutDuration => fadeOutDuration;
    }
}
