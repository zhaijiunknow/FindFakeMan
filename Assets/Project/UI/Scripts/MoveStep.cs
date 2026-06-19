using UnityEngine;
using DG.Tweening;

public class MoveStep : AnimationStep
{
    public enum MoveType
    {
        AnchorPosition,
        LocalPosition,
        WorldPosition
    }

    [Header("移动参数")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private MoveType moveType = MoveType.AnchorPosition;
    [SerializeField] private float moveDuration = 1f;
    [SerializeField] private Ease moveEase = Ease.OutQuad;

    public override Tween GetTween()
    {
        if (targetTransform == null)
        {
            Debug.LogError("MoveStep: 缺少目标 Transform");
            return null;
        }

        switch (moveType)
        {
            case MoveType.AnchorPosition:
                if (transform is RectTransform selfRect && targetTransform is RectTransform targetRect)
                    return selfRect.DOAnchorPos(targetRect.anchoredPosition, moveDuration).SetEase(moveEase);
                Debug.LogWarning("MoveStep: AnchorPosition 需要当前对象和目标对象都为 RectTransform，已自动降级为 LocalPosition");
                return transform.DOLocalMove(targetTransform.localPosition, moveDuration).SetEase(moveEase);

            case MoveType.LocalPosition:
                return transform.DOLocalMove(targetTransform.localPosition, moveDuration).SetEase(moveEase);

            case MoveType.WorldPosition:
                return transform.DOMove(targetTransform.position, moveDuration).SetEase(moveEase);

            default:
                return null;
        }
    }
}
