using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;

/// <summary>
/// 控制 AnimationStep 的收集、分组与播放。
/// </summary>
public class SequenceController : MonoBehaviour
{
    [Header("步骤缓存")]
    [Tooltip("自动收集当前场景中的 AnimationStep，无需手动拖拽")]
    [SerializeField] private List<AnimationStep> manualSteps = new();

    [Header("播放设置")]
    [SerializeField] private bool playOnce = true;
    [SerializeField] private bool logSequence = true;

    private Sequence mainSequence;
    private bool hasPlayed = false;

    private void Reset()
    {
        RefreshManualStepsCache();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        RefreshManualStepsCache();
    }

    private void Start()
    {
        BuildSequence();
    }

    private List<AnimationStep> CollectSteps()
    {
        return FindObjectsByType<AnimationStep>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(step => step != null && step.gameObject.scene == gameObject.scene)
            .OrderBy(step => step.GroupName)
            .ThenBy(step => step.OrderInGroup)
            .ToList();
    }

    private void RefreshManualStepsCache()
    {
        var steps = CollectSteps();
        if (manualSteps != null && manualSteps.SequenceEqual(steps))
            return;

        manualSteps = steps;
    }

    private void BuildSequence()
    {
        RefreshManualStepsCache();

        if (mainSequence != null)
        {
            mainSequence.Kill();
            mainSequence = null;
        }

        if (manualSteps.Count == 0)
        {
            Debug.LogWarning("没有找到任何 AnimationStep，无法构建序列。");
            return;
        }

        if (logSequence)
        {
            LogSequenceGroups(manualSteps);
        }

        mainSequence = DOTween.Sequence();
        mainSequence.Pause();
        mainSequence.SetAutoKill(false);

        bool hasValidGroup = false;
        foreach (var group in manualSteps.GroupBy(step => step.GroupName))
        {
            Sequence groupSequence = BuildGroupSequence(group);
            if (groupSequence == null)
                continue;

            mainSequence.Join(groupSequence);
            hasValidGroup = true;
        }

        if (!hasValidGroup)
        {
            mainSequence.Kill();
            mainSequence = null;
            Debug.LogWarning("没有可播放的有效 AnimationStep。");
        }
    }

    private Sequence BuildGroupSequence(IEnumerable<AnimationStep> groupSteps)
    {
        Sequence groupSequence = DOTween.Sequence();
        bool hasValidOrder = false;

        foreach (var orderGroup in groupSteps.GroupBy(step => step.OrderInGroup).OrderBy(group => group.Key))
        {
            Sequence orderSequence = DOTween.Sequence();
            bool hasValidTween = false;

            foreach (var step in orderGroup)
            {
                Tween tween = step.GetTween();
                if (tween == null)
                {
                    Debug.LogWarning($"步骤 {step.name} 未生成有效 Tween，已跳过。");
                    continue;
                }

                orderSequence.Insert(Mathf.Max(0f, step.DelayBefore), tween);
                hasValidTween = true;
            }

            if (!hasValidTween)
                continue;

            groupSequence.Append(orderSequence);
            hasValidOrder = true;
        }

        if (!hasValidOrder)
        {
            groupSequence.Kill();
            return null;
        }

        return groupSequence;
    }

    private void LogSequenceGroups(List<AnimationStep> steps)
    {
        Debug.Log("==== 动画分组顺序 ====");

        foreach (var group in steps.GroupBy(step => step.GroupName))
        {
            Debug.Log($"Group: {group.Key}（与其它组同时开始）");

            foreach (var orderGroup in group.GroupBy(step => step.OrderInGroup).OrderBy(order => order.Key))
            {
                Debug.Log($"  Order {orderGroup.Key}（同 Order 并行）");

                foreach (var step in orderGroup)
                {
                    Debug.Log($"    - 延迟: {step.DelayBefore}, 对象: {step.name}");
                }
            }
        }
    }

    private void Update()
    {
        if (playOnce && hasPlayed)
            return;

        if (Input.anyKeyDown)
        {
            PlaySequence();
        }
    }

    public void PlaySequence()
    {
        if (mainSequence == null)
            return;

        mainSequence.Rewind();
        mainSequence.Play();
        hasPlayed = true;
    }
}
