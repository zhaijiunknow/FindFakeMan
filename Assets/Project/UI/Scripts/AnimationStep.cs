using UnityEngine;
using DG.Tweening;

public abstract class AnimationStep : MonoBehaviour
{
    [Header("分组信息")]
    [SerializeField] protected string groupName = "Default";
    [SerializeField] protected int orderInGroup = 0;

    public string GroupName => groupName;
    public int OrderInGroup => orderInGroup;

    /// <summary>
    /// 子类必须重写，返回该步骤对应的 Tween 动画。
    /// </summary>
    public abstract Tween GetTween();
}