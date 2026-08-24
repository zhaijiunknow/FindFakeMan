using DG.Tweening;
using UnityEngine;

/// <summary>
/// 缩放步骤（AnimationStep）：对目标做 DOScale（配合正交 UI 场景的"伪镜头"拉远）。
/// 与 MoveStep 同层，SequenceController 收集播放。
/// </summary>
public class ScaleStep : AnimationStep
{
    [Header("缩放参数")]
    [Tooltip("目标 Transform；为空则缩自身")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Vector3 targetScale = Vector3.one * 0.6f;
    [SerializeField] private float scaleDuration = 1f;
    [SerializeField] private Ease scaleEase = Ease.OutQuad;

    [Header("抽帧缩放（步进式，可选）")]
    [SerializeField] private bool useStepped = false;
    [SerializeField] private int steps = 5;
    [SerializeField] private float stepHoldDuration = 0.15f;

    public override Tween GetTween()
    {
        var target = targetTransform != null ? targetTransform : transform;
        if (target == null)
        {
            Debug.LogError("ScaleStep: 缺少目标 Transform");
            return null;
        }

        if (useStepped)
        {
            return CreateSteppedTween(target);
        }

        return target.DOScale(targetScale, scaleDuration).SetEase(scaleEase);
    }

    private Tween CreateSteppedTween(Transform target)
    {
        var startScale = target.localScale;
        int actualSteps = Mathf.Max(1, steps);

        Sequence seq = DOTween.Sequence();
        seq.AppendCallback(() => target.localScale = startScale);

        for (int i = 1; i <= actualSteps; i++)
        {
            seq.AppendInterval(stepHoldDuration);
            float progress = (float)i / actualSteps;
            Vector3 next = Vector3.Lerp(startScale, targetScale, progress);
            seq.Append(target.DOScale(next, stepHoldDuration).SetEase(scaleEase));
        }

        return seq;
    }
}
