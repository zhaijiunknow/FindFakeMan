using UnityEngine;
using DG.Tweening;

public abstract class AnimationStep : MonoBehaviour
{
    [Header("������Ϣ")]
    [SerializeField] protected string groupName = "Default";
    [SerializeField] protected int orderInGroup = 0;
    [SerializeField] protected float delayBefore = 0f;

    public string GroupName => groupName;
    public int OrderInGroup => orderInGroup;
    public float DelayBefore => delayBefore;

    /// <summary>
    /// ���������д�����ظò����Ӧ�� Tween ������
    /// </summary>
    public abstract Tween GetTween();
}