using UnityEngine;
using TMPro;
using DG.Tweening;

public class ShowTextStep : AnimationStep
{
    [Header("显示文本设置")]
    [SerializeField] private TextMeshProUGUI textComponent;
    [SerializeField] private string textContent;
    [SerializeField] private float textFadeInDuration = 0.5f;
    [SerializeField] private Ease textEase = Ease.OutQuad;

    public override Tween GetTween()
    {
        if (textComponent == null)
        {
            Debug.LogError("ShowTextStep: 缺少 TextMeshProUGUI 组件");
            return null;
        }

        textComponent.text = textContent;
        return textComponent.DOFade(1f, textFadeInDuration)
            .From(0f)
            .SetEase(textEase);
    }
}