using UnityEngine;
using DG.Tweening;

public class FadeStep : AnimationStep
{
    [Header("透明度设置")]
    [SerializeField] private CanvasGroup targetCanvasGroup;
    [SerializeField] private float targetAlpha = 1f;
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private Ease fadeEase = Ease.OutQuad;
    [SerializeField] private float startAlphaOffset = -1f; // 小于0时使用当前透明度

    public override Tween GetTween()
    {
        if (targetCanvasGroup == null)
        {
            Debug.LogError("FadeStep: 缺少 CanvasGroup");
            return null;
        }

        if (startAlphaOffset >= 0f)
            return targetCanvasGroup.DOFade(targetAlpha, fadeDuration)
                .From(startAlphaOffset)
                .SetEase(fadeEase);
        else
            return targetCanvasGroup.DOFade(targetAlpha, fadeDuration)
                .SetEase(fadeEase);
    }
}