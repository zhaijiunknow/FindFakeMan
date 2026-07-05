using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class UIWindowManager : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform closedUI;         // 图标 RectTransform（右上角 anchor）
    public RectTransform openedUI;         // 展开窗口 RectTransform（中心 anchor）
    public CanvasGroup openedUIGroup;      // 用于渐变透明

    [Header("动画设置")]
    public float animationDuration = 0.4f;

    private Vector2 savedOpenedPos;
    private Vector3 savedOpenedScale;
    private Vector2 closedSize;

    private RectTransform canvasRect;

    private void Awake()
    {
        canvasRect = openedUI.GetComponentInParent<Canvas>().GetComponent<RectTransform>();

        closedSize = closedUI.sizeDelta;
        savedOpenedPos = openedUI.anchoredPosition;
        savedOpenedScale = openedUI.localScale;
        openedUIGroup.alpha = 0f;
        openedUI.gameObject.SetActive(false);
    }

    public void OnClickClosedChat()
    {
        Expand();
    }

    public void OnClickClose()
    {
        Collapse();
    }

    public void Expand()
    {
        // 获取 closedChat 的世界中心点
        Vector3 worldPos = closedUI.TransformPoint(closedUI.rect.center);

        // 将世界点转换为 openedChat 所在父级的本地坐标（通常是 Canvas）
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(openedUI, worldPos, null, out localPoint);

        // 启用并设置 openedChat 起始状态
        openedUI.gameObject.SetActive(true);
        openedUI.anchoredPosition = localPoint;

        // 缩放比例
        Vector2 openedSize = openedUI.sizeDelta;
        float scaleX = closedSize.x / openedSize.x;
        float scaleY = closedSize.y / openedSize.y;
        openedUI.localScale = new Vector3(scaleX, scaleY, 1f);
        openedUIGroup.alpha = 0f;

        // 播放展开动画
        openedUI.DOAnchorPos(savedOpenedPos, animationDuration);
        openedUI.DOScale(Vector3.one, animationDuration);
        openedUIGroup.DOFade(1f, animationDuration);

        //closedUI.DOScale(Vector3.zero, animationDuration).OnComplete(() =>
        //{
        //    closedUI.gameObject.SetActive(false);
        //    closedUI.localScale = Vector3.one;
        //});

    }

    public void Collapse()
    {
        // 记录 openedChat 当前锚点位置
        savedOpenedPos = openedUI.anchoredPosition;
        savedOpenedScale = openedUI.localScale;

        // 获取 closedChat 的世界中心点
        Vector3 worldPos = closedUI.TransformPoint(closedUI.rect.center);
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, worldPos, null, out localPoint);

        // 缩放比例
        Vector2 openedSize = openedUI.sizeDelta;
        float scaleX = closedSize.x / openedSize.x;
        float scaleY = closedSize.y / openedSize.y;

        // 播放收起动画
        openedUI.DOAnchorPos(localPoint, animationDuration);
        openedUI.DOScale(new Vector3(scaleX, scaleY, 1f), animationDuration);
        openedUIGroup.DOFade(0f, animationDuration).OnComplete(() =>
        {
            openedUI.gameObject.SetActive(false);
        });

        // 显示并动画恢复 closedChat
        //closedUI.gameObject.SetActive(true);
        //closedUI.localScale = Vector3.zero;
        //closedUI.DOScale(Vector3.one, animationDuration);
    }

}
