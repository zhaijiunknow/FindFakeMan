using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class FadeStep : AnimationStep
{
    [Header("透明度参数")]
    [SerializeField] private CanvasGroup targetCanvasGroup;
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private float targetAlpha = 1f;
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private Ease fadeEase = Ease.OutQuad;
    [SerializeField] private float startAlphaOffset = -1f;

    public override Tween GetTween()
    {
        if (targetGraphic != null)
        {
            return CreateGraphicTween(targetGraphic);
        }

        if (targetCanvasGroup != null)
        {
            return CreateCanvasGroupTween(targetCanvasGroup);
        }

        Debug.LogError("FadeStep: 缺少 CanvasGroup 或 Graphic");
        return null;
    }

    private Tween CreateCanvasGroupTween(CanvasGroup canvasGroup)
    {
        if (startAlphaOffset >= 0f)
        {
            return canvasGroup.DOFade(targetAlpha, fadeDuration)
                .From(startAlphaOffset)
                .SetEase(fadeEase);
        }

        return canvasGroup.DOFade(targetAlpha, fadeDuration)
            .SetEase(fadeEase);
    }

    private Tween CreateGraphicTween(Graphic graphic)
    {
        if (startAlphaOffset >= 0f)
        {
            return graphic.DOFade(targetAlpha, fadeDuration)
                .From(startAlphaOffset)
                .SetEase(fadeEase);
        }

        return graphic.DOFade(targetAlpha, fadeDuration)
            .SetEase(fadeEase);
    }
}
