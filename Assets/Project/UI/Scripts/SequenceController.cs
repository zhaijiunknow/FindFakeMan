using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;

/// <summary>
/// 总控脚本：按任意键后，按分组及组内序号顺序播放所有 AnimationStep。
/// </summary>
public class SequenceController : MonoBehaviour
{
    [Header("步骤来源")]
    [Tooltip("拖入场景中的 AnimationStep 对象，或留空以自动从子对象获取")]
    [SerializeField] private List<AnimationStep> manualSteps;

    [Header("播放设置")]
    [SerializeField] private bool playOnce = true;           // 是否只播放一次
    [SerializeField] private bool logSequence = true;        // 是否打印播放顺序

    private Sequence mainSequence;
    private bool hasPlayed = false;

    private void Start()
    {
        // 准备步骤列表：优先使用手动拖入的，若没有则从子对象自动获取
        List<AnimationStep> steps = manualSteps != null && manualSteps.Count > 0
            ? new List<AnimationStep>(manualSteps)
            : GetComponentsInChildren<AnimationStep>().ToList();

        if (steps.Count == 0)
        {
            Debug.LogWarning("没有找到任何 AnimationStep，无法播放序列。");
            return;
        }

        // 排序：先按分组名字母顺序，组内再按序号升序
        var sortedSteps = steps
            .OrderBy(s => s.GroupName)
            .ThenBy(s => s.OrderInGroup)
            .ToList();

        if (logSequence)
        {
            Debug.Log("==== 动画播放顺序 ====");
            for (int i = 0; i < sortedSteps.Count; i++)
            {
                var s = sortedSteps[i];
                Debug.Log($"{i + 1}. 分组: {s.GroupName}, 序号: {s.OrderInGroup}, 对象: {s.name}");
            }
        }

        // 构建 DOTween Sequence，按顺序追加每个步骤的 Tween
        mainSequence = DOTween.Sequence();
        mainSequence.Pause(); // 先暂停，等待按键触发
        mainSequence.SetAutoKill(false); // 保留序列以便重复使用

        foreach (var step in sortedSteps)
        {
            Tween tween = step.GetTween();
            if (tween != null)
            {
                mainSequence.Append(tween);
            }
            else
            {
                // 如果某个步骤返回 null（配置错误），跳过
                Debug.LogWarning($"步骤 {step.name} 未生成有效动画，已跳过。");
            }
        }
    }

    private void Update()
    {
        // 如果已经播放过且设置了只播放一次，则不再响应
        if (playOnce && hasPlayed)
            return;

        // 检测任意键按下
        if (Input.anyKeyDown)
        {
            PlaySequence();
        }
    }

    public void PlaySequence()
    {
        if (mainSequence == null)
            return;

        // 如果序列正在播放，先停止并重置（避免重叠）
        mainSequence.Rewind();
        mainSequence.Play();
        hasPlayed = true;
    }
}