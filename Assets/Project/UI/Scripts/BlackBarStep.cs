using UnityEngine;
using DG.Tweening;

/// <summary>
/// 上下黑边（Letterbox）步骤：黑边快速移入遮住屏幕上下边缘，停留后逐渐移出。
/// 与 MoveStep 放在同一分组（groupName / OrderInGroup）即可在移动时同步播放。
/// </summary>
public class BlackBarStep : AnimationStep
{
    [Header("黑边引用")]
    [Tooltip("顶部黑边。场景里的摆放位置即屏幕外的原始位置，动画时向下偏移 hideOffset 移入屏幕（出现）")]
    [SerializeField] private RectTransform topBar;
    [Tooltip("底部黑边。场景里的摆放位置即屏幕外的原始位置，动画时向上偏移 hideOffset 移入屏幕（出现）")]
    [SerializeField] private RectTransform bottomBar;

    [Header("黑边参数")]
    [Tooltip("黑边单程移动距离（像素）：从屏幕外的原始位置移入 hideOffset 出现，再移回 hideOffset 离开")]
    [SerializeField] private float hideOffset = 100f;

    [Header("动画参数")]
    [Tooltip("黑边快速移入（出现）时长（秒）")]
    [SerializeField] private float appearDuration = 0.2f;
    [SerializeField] private Ease appearEase = Ease.OutQuad;

    [Tooltip("黑边完全遮住后的停留时长（秒），0 表示不停留")]
    [SerializeField] private float holdDuration = 0f;

    [Tooltip("黑边逐渐移出（离开）时长（秒）")]
    [SerializeField] private float leaveDuration = 0.8f;
    [SerializeField] private Ease leaveEase = Ease.InOutSine;

    public override Tween GetTween()
    {
        if (topBar == null || bottomBar == null)
        {
            Debug.LogError("BlackBarStep: 缺少顶部或底部黑边 RectTransform");
            return null;
        }

        // 黑边在场景里的原始位置 = 屏幕外（隐藏停留位置）
        Vector2 topRest = topBar.anchoredPosition;
        Vector2 bottomRest = bottomBar.anchoredPosition;

        // 出现位置 = 原始位置向内偏移 hideOffset（顶部向下、底部向上）
        Vector2 topShown = new Vector2(topRest.x, topRest.y - hideOffset);
        Vector2 bottomShown = new Vector2(bottomRest.x, bottomRest.y + hideOffset);

        Sequence seq = DOTween.Sequence();

        // 1. 先确保处于屏幕外的原始位置
        seq.AppendCallback(() =>
        {
            topBar.anchoredPosition = topRest;
            bottomBar.anchoredPosition = bottomRest;
        });

        // 2. 快速移入 hideOffset（出现）
        seq.Append(topBar.DOAnchorPos(topShown, appearDuration).SetEase(appearEase));
        seq.Join(bottomBar.DOAnchorPos(bottomShown, appearDuration).SetEase(appearEase));

        // 3. 可选停留
        if (holdDuration > 0f)
            seq.AppendInterval(holdDuration);

        // 4. 逐渐移回原始位置（离开）
        seq.Append(topBar.DOAnchorPos(topRest, leaveDuration).SetEase(leaveEase));
        seq.Join(bottomBar.DOAnchorPos(bottomRest, leaveDuration).SetEase(leaveEase));

        return seq;
    }
}
