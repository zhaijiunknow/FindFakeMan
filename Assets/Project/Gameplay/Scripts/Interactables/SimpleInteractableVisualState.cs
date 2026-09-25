using Project.Gameplay.Scripts.Interactables;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Gameplay.Scripts.Interactables
{
    /// <summary>
    /// 交互物被收集/失效后的表现：把对应的房间图层压暗，并关掉点击，避免玩家重复点已经查过的东西。
    ///
    /// 现在"图自己就是按钮"（alphaHitTest），所以 <see cref="hitArea"/> 和 <see cref="targetLayer"/>
    /// 通常指向同一张 Image：压暗表现 + 不再接收点击都作用在它身上。
    ///
    /// 为什么用轮询而不是事件：<see cref="SimpleInteractable"/> 目前没有「状态变化」事件，
    /// 状态是被 SampleInteractableRule / InteractionManager 直接改的，所以这里每帧读一次（交互物只有几个，开销可以忽略）。
    /// </summary>
    public sealed class SimpleInteractableVisualState : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private SimpleInteractable interactable;
        [Tooltip("要压暗的房间图层（整帧 Image，通常就是这个交互物自己）。")]
        [SerializeField] private Image targetLayer;
        [Tooltip("命中判定所在的图（收集后关掉 raycastTarget，防止重复点击）。")]
        [SerializeField] private Image hitArea;

        [Header("表现")]
        [Tooltip("已收集后是否把图层压暗。**默认关闭**：查过就一直半透明会让人以为东西没了。")]
        [SerializeField] private bool dimWhenCollected;
        [Tooltip("压暗到什么透明度（只在 dimWhenCollected 打开时有效）。")]
        [SerializeField, Range(0f, 1f)] private float collectedAlpha = 0.55f;
        [Tooltip("是否在已收集后同时禁用命中区。")]
        [SerializeField] private bool disableHitAreaWhenCollected = true;

        private bool applied;

        private void Awake()
        {
            if (interactable == null)
            {
                interactable = GetComponentInParent<SimpleInteractable>();
            }
        }

        private void Update()
        {
            if (applied || interactable == null)
            {
                return;
            }

            if (!(interactable.IsCollected || !interactable.IsActive))
            {
                return;
            }

            applied = true;
            Apply();
        }

        private void Apply()
        {
            // 压暗改成**可选项**（默认关）：默认只做"不再接收点击"，不动美术颜色。
            // 之前那个"每帧校正"是为了防悬停高亮把压暗盖掉，现在不压暗了，也就退回只做一次。
            if (dimWhenCollected && targetLayer != null)
            {
                var color = targetLayer.color;
                targetLayer.color = new Color(color.r, color.g, color.b, collectedAlpha);
            }

            if (disableHitAreaWhenCollected && hitArea != null)
            {
                // **不再关掉射线** ✗→✓：老设计是"收集后防止重复点击"✗，
                // 但现在的口径正好相反 ✓ —— 读过一次只是**解锁收容物** ✓，
                // 玩家必须还能再点它、才能「拾取」✓。
                // 关掉射线会出现"只有那件家具点不动"✗✗ —— 而且因为只有**成功读过**的家具会被标记 ✓，
                // 表现就是"就它点不动、别的都行"✓（用户实测就是这个 ✗）。
                // 要不要变淡由 `dimWhenCollected` 决定 ✓ —— 那是显示问题 ✓，和能不能点无关 ✓。
            }
        }
    }
}
