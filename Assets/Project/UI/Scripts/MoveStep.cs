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
    [SerializeField] private float moveDuration = 1f;              // 连续移动或均匀抽帧的总时长
    [SerializeField] private Ease moveEase = Ease.OutQuad;

    [Header("抽帧移动（步进式）")]
    [SerializeField] private bool useSteppedMovement = false;
    [SerializeField] private int steps = 5;                       // 跳跃步数

    [Tooltip("每一步停顿后，快速移动到下一帧所用的时长（秒）。值越小，顿一下后窜得越急促")]
    [SerializeField] private float stepMoveDuration = 0.15f;

    [Tooltip("启用后，每一步的等待时间将固定为下方设定的值（忽略总时长）")]
    [SerializeField] private bool useCustomStepDuration = false;

    [Tooltip("每一步的等待时长（秒）。只在 useCustomStepDuration = true 时生效")]
    [SerializeField] private float customStepDuration = 0.1f;

    public override Tween GetTween()
    {
        if (targetTransform == null)
        {
            Debug.LogError("MoveStep: 缺少目标 Transform");
            return null;
        }

        if (useSteppedMovement)
            return CreateSteppedTween();

        // ---- 原有连续移动逻辑 ----
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

    private Tween CreateSteppedTween()
    {
        Vector3 startPos, endPos;

        // 1. 获取起点和终点坐标
        switch (moveType)
        {
            case MoveType.AnchorPosition:
                if (transform is RectTransform selfRect && targetTransform is RectTransform targetRect)
                {
                    startPos = selfRect.anchoredPosition;
                    endPos = targetRect.anchoredPosition;
                }
                else
                {
                    startPos = transform.localPosition;
                    endPos = targetTransform.localPosition;
                }
                break;

            case MoveType.LocalPosition:
                startPos = transform.localPosition;
                endPos = targetTransform.localPosition;
                break;

            case MoveType.WorldPosition:
                startPos = transform.position;
                endPos = targetTransform.position;
                break;

            default:
                return null;
        }

        Sequence seq = DOTween.Sequence();
        seq.AppendCallback(() => SetPosition(startPos));

        int actualSteps = Mathf.Max(1, steps);

        // 2. 根据是否自定义停顿时间，决定每一步的间隔
        float stepInterval;
        if (useCustomStepDuration)
        {
            stepInterval = customStepDuration;   // 固定时长，总时长 = steps * customStepDuration
        }
        else
        {
            stepInterval = moveDuration / actualSteps;   // 均匀分配总时长
        }

        // 3. 构建步进动画：每步先停顿，再快速移动到下一帧，形成“顿一下再窜出”的效果
        for (int i = 1; i <= actualSteps; i++)
        {
            seq.AppendInterval(stepInterval);

            float progress = (float)i / actualSteps;
            Vector3 nextPos = Vector3.Lerp(startPos, endPos, progress);

            Tween moveTween = CreateMoveTo(nextPos, stepMoveDuration);
            if (moveTween != null)
                seq.Append(moveTween);
        }

        return seq;
    }

    private void SetPosition(Vector3 pos)
    {
        if (transform == null) return;

        switch (moveType)
        {
            case MoveType.AnchorPosition:
                if (transform is RectTransform rect)
                    rect.anchoredPosition = pos;
                else
                    transform.localPosition = pos;
                break;

            case MoveType.LocalPosition:
                transform.localPosition = pos;
                break;

            case MoveType.WorldPosition:
                transform.position = pos;
                break;
        }
    }

    private Tween CreateMoveTo(Vector3 pos, float duration)
    {
        if (transform == null) return null;

        switch (moveType)
        {
            case MoveType.AnchorPosition:
                if (transform is RectTransform rect)
                    return rect.DOAnchorPos(pos, duration).SetEase(moveEase);
                return transform.DOLocalMove(pos, duration).SetEase(moveEase);

            case MoveType.LocalPosition:
                return transform.DOLocalMove(pos, duration).SetEase(moveEase);

            case MoveType.WorldPosition:
                return transform.DOMove(pos, duration).SetEase(moveEase);

            default:
                return null;
        }
    }
}