using UnityEngine;
using DG.Tweening;

/// <summary>
/// 摄像机视野（FOV）变化步骤，支持连续变化与抽帧步进变化。
/// </summary>
public class FovStep : AnimationStep
{
    [Header("视野参数")]
    [Tooltip("目标摄像机。为空时自动使用 Camera.main")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float targetFov = 60f;
    [SerializeField] private float fovDuration = 1f;
    [SerializeField] private Ease fovEase = Ease.OutQuad;

    [Tooltip("起始视野。>= 0 时从该值变化到 targetFov，< 0 则从当前视野开始")]
    [SerializeField] private float startFov = -1f;

    [Header("抽帧变化（步进式）")]
    [SerializeField] private bool useSteppedMovement = false;
    [SerializeField] private int steps = 5;                       // 变化步数

    [Tooltip("每一步停顿后，快速变到下一帧所用的时长（秒）。值越小，顿一下后变化越急促")]
    [SerializeField] private float stepFovDuration = 0.15f;

    [Tooltip("启用后，每一步的等待时间将固定为下方设定的值（忽略总时长）")]
    [SerializeField] private bool useCustomStepDuration = false;

    [Tooltip("每一步的等待时长（秒）。只在 useCustomStepDuration = true 时生效")]
    [SerializeField] private float customStepDuration = 0.1f;

    [Header("脉冲式（快速到目标值后缓慢回原值）")]
    [Tooltip("开启后：快速变化到 targetFov，再缓慢回到原值（startFov >= 0 时用 startFov，否则用当前视野）")]
    [SerializeField] private bool usePulsePattern = false;
    [Tooltip("从原值到 targetFov 所用时长（秒）。值越小越急促")]
    [SerializeField] private float pulseExpandDuration = 0.2f;
    [SerializeField] private Ease pulseExpandEase = Ease.OutQuad;
    [Tooltip("从 targetFov 缓慢回到原值所用时长（秒）")]
    [SerializeField] private float pulseReturnDuration = 0.8f;
    [SerializeField] private Ease pulseReturnEase = Ease.InOutSine;

    private Camera ResolvedCamera
    {
        get
        {
            if (targetCamera != null)
                return targetCamera;
            return Camera.main;
        }
    }

    public override Tween GetTween()
    {
        Camera cam = ResolvedCamera;
        if (cam == null)
        {
            Debug.LogError("FovStep: 缺少目标 Camera");
            return null;
        }

        if (useSteppedMovement)
            return CreateSteppedTween(cam);

        if (usePulsePattern)
            return CreatePulseTween(cam);

        // ---- 连续变化 ----
        // 注意：From(float) 是定义在具体 TweenerCore 类型上的泛型扩展，
        // 因此必须链式调用（或保持具体类型），不能先赋给基类 Tween。
        var tween = cam.DOFieldOfView(targetFov, fovDuration).SetEase(fovEase);
        if (startFov >= 0f)
            return tween.From(startFov);
        return tween;
    }

    private Tween CreateSteppedTween(Camera cam)
    {
        float startFovValue = startFov >= 0f ? startFov : cam.fieldOfView;
        float endFov = targetFov;

        Sequence seq = DOTween.Sequence();
        seq.AppendCallback(() => cam.fieldOfView = startFovValue);

        int actualSteps = Mathf.Max(1, steps);

        // 根据是否自定义停顿时间，决定每一步的间隔
        float stepInterval;
        if (useCustomStepDuration)
        {
            stepInterval = customStepDuration;   // 固定时长，总时长 = steps * customStepDuration
        }
        else
        {
            stepInterval = fovDuration / actualSteps;   // 均匀分配总时长
        }

        // 每步先停顿，再快速变到下一帧，形成“顿一下再窜出”的效果
        for (int i = 1; i <= actualSteps; i++)
        {
            seq.AppendInterval(stepInterval);

            float progress = (float)i / actualSteps;
            float nextFov = Mathf.Lerp(startFovValue, endFov, progress);

            seq.Append(cam.DOFieldOfView(nextFov, stepFovDuration).SetEase(fovEase));
        }

        return seq;
    }

    /// <summary>
    /// 脉冲式：快速变化到 targetFov，再缓慢回到原值。
    /// </summary>
    private Tween CreatePulseTween(Camera cam)
    {
        // 原值：配置了 startFov 就用它，否则取动画开始前的当前视野
        float originalFov = startFov >= 0f ? startFov : cam.fieldOfView;

        Sequence seq = DOTween.Sequence();
        seq.Append(cam.DOFieldOfView(targetFov, pulseExpandDuration).SetEase(pulseExpandEase));
        seq.Append(cam.DOFieldOfView(originalFov, pulseReturnDuration).SetEase(pulseReturnEase));
        return seq;
    }
}
