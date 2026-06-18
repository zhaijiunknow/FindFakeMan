using UnityEngine;
using DG.Tweening;

public class MoveStep : AnimationStep
{
    public enum MoveType
    {
        AnchorPosition,  // UI 推荐：修改 anchoredPosition
        LocalPosition,   // 修改 localPosition
        WorldPosition    // 修改 worldPosition
    }

    [Header("移动设置")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private MoveType moveType = MoveType.AnchorPosition;
    [SerializeField] private Vector3 targetPosition;
    [SerializeField] private float moveDuration = 1f;
    [SerializeField] private Ease moveEase = Ease.OutQuad;

    public override Tween GetTween()
    {
        if (targetTransform == null)
        {
            Debug.LogError("MoveStep: 缺少目标 Transform");
            return null;
        }

        // 自动检测：如果目标是 RectTransform 且移动类型未手动改变，则默认使用 AnchorPosition
        if (moveType == MoveType.AnchorPosition && targetTransform is RectTransform rt)
        {
            return rt.DOAnchorPos(targetPosition, moveDuration).SetEase(moveEase);
        }

        switch (moveType)
        {
            case MoveType.AnchorPosition:
                if (targetTransform is RectTransform rect)
                    return rect.DOAnchorPos(targetPosition, moveDuration).SetEase(moveEase);
                Debug.LogWarning("目标不是 RectTransform，AnchorPosition 不可用，已降级为 LocalPosition");
                return targetTransform.DOLocalMove(targetPosition, moveDuration).SetEase(moveEase);

            case MoveType.LocalPosition:
                return targetTransform.DOLocalMove(targetPosition, moveDuration).SetEase(moveEase);

            case MoveType.WorldPosition:
                return targetTransform.DOMove(targetPosition, moveDuration).SetEase(moveEase);

            default:
                return null;
        }
    }
}