using UnityEngine;
using DG.Tweening;

/// <summary>
/// 播放 UIWindowManager 窗口展开/收拢动画的步骤。
/// 加入 SequenceController 序列即可在播放时执行窗口的 open/close 过程。
/// 注意：需排在激活窗口根物体的 SetActiveStep 之后（order 更大），确保窗口已被激活。
/// </summary>
public class UIWindowStep : AnimationStep
{
    public enum WindowAction
    {
        Open,    // UIWindowManager.Expand()：展开
        Close    // UIWindowManager.Collapse()：收拢
    }

    [Header("窗口参数")]
    [Tooltip("目标窗口。为空时自动查找同物体上的 UIWindowManager")]
    [SerializeField] private UIWindowManager targetWindow;
    [SerializeField] private WindowAction action = WindowAction.Open;

    public override Tween GetTween()
    {
        UIWindowManager window = targetWindow != null ? targetWindow : GetComponent<UIWindowManager>();
        if (window == null)
        {
            Debug.LogError("UIWindowStep: 缺少目标 UIWindowManager");
            return null;
        }

        float duration = Mathf.Max(0f, window.animationDuration);

        // 注意：GetTween 在序列构建阶段就被调用，必须把窗口动画延迟到序列播放时再触发
        Sequence seq = DOTween.Sequence();
        seq.AppendCallback(() =>
        {
            if (action == WindowAction.Open)
                window.Expand();
            else
                window.Collapse();
        });
        seq.AppendInterval(duration);

        return seq;
    }
}
