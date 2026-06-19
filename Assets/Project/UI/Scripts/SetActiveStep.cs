using UnityEngine;
using DG.Tweening;

public class SetActiveStep : AnimationStep
{
    [Header("激活参数")]
    [SerializeField] private GameObject targetObject;
    [SerializeField] private bool activeState = true;

    public override Tween GetTween()
    {
        GameObject target = targetObject != null ? targetObject : gameObject;
        return DOVirtual.DelayedCall(0f, () => target.SetActive(activeState));
    }
}
