using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using DG.Tweening;

public class UIWindowManager : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform closedUI;         // 收起状态（最小化/任务栏小图）锚点
    public RectTransform openedUI;         // 展开状态（窗口主体）
    public CanvasGroup openedUIGroup;      // 窗口透明度

    [Header("动画")]
    public float animationDuration = 0.4f;

    [Header("初始状态")]
    [Tooltip("运行开始时窗口的初始状态：默认隐藏（false）；勾选后初始直接显示（true）")]
    [SerializeField] private bool startVisible = false;

    private Vector2 savedOpenedPos;
    private Vector3 savedOpenedScale;
    private Vector2 closedSize;
    private RectTransform canvasRect;

    // 窗口三态（铺满 ⇄ 窗口）记录
    private Vector2 normalAnchorMin, normalAnchorMax, normalOffsetMin, normalOffsetMax;
    private bool isFull;

    /// <summary>连点防堆积：先杀掉 openedUI/openedUIGroup 上的旧动画。</summary>
    private void KillTweens()
    {
        if (openedUI != null) DOTween.Kill(openedUI);
        if (openedUIGroup != null) DOTween.Kill(openedUIGroup);
    }

    private void Awake()
    {
        canvasRect = openedUI.GetComponentInParent<Canvas>().GetComponent<RectTransform>();

        closedSize = closedUI.sizeDelta;
        savedOpenedPos = openedUI.anchoredPosition;
        savedOpenedScale = openedUI.localScale;
        if (startVisible)
        {
            openedUI.gameObject.SetActive(true);
            openedUI.anchoredPosition = savedOpenedPos;
            openedUI.localScale = Vector3.one;
            openedUIGroup.alpha = 1f;
        }
        else
        {
            openedUIGroup.alpha = 0f;
            openedUI.gameObject.SetActive(false);
        }

        // 通用窗口三键：red=关闭，green=最大化(铺满)，blue=窗口化(还原)。任意窗口标题栏带这些键即生效。
        BindButton("red", OnCloseWindow);
        BindButton("green", OnMaximize);
        BindButton("blue", OnRestore);
    }

    private void BindButton(string childName, UnityAction action)
    {
        var t = GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == childName);
        t?.GetComponent<Button>()?.onClick.AddListener(action);
    }

    // ---------- 窗口三态 ----------

    public void OnClickClosedChat() => Expand();
    public void OnClickClose() => Collapse();

    /// <summary>关闭：收起（red）。</summary>
    public void OnCloseWindow() => Collapse();

    /// <summary>最大化：窗口 → 铺满（green）。</summary>
    public void OnMaximize()
    {
        if (openedUI == null || isFull) return;
        RecordOpened();
        openedUI.anchorMin = Vector2.zero;
        openedUI.anchorMax = Vector2.one;
        openedUI.offsetMin = Vector2.zero;
        openedUI.offsetMax = Vector2.zero;
        isFull = true;
    }

    /// <summary>窗口化：铺满 → 还原窗口（blue）。</summary>
    public void OnRestore()
    {
        if (openedUI == null || !isFull) return;
        openedUI.anchorMin = normalAnchorMin;
        openedUI.anchorMax = normalAnchorMax;
        openedUI.offsetMin = normalOffsetMin;
        openedUI.offsetMax = normalOffsetMax;
        isFull = false;
    }

    private void RecordOpened()
    {
        if (openedUI == null) return;
        normalAnchorMin = openedUI.anchorMin;
        normalAnchorMax = openedUI.anchorMax;
        normalOffsetMin = openedUI.offsetMin;
        normalOffsetMax = openedUI.offsetMax;
    }

    // ---------- 展开 / 收起 ----------

    public void Expand()
    {
        KillTweens();
        // closedUI / closedSize 是"从哪长出来"的锚点与起始尺寸，都是可选的：
        // 预制体里 closedUI 是空的 ✗ —— 不判空的话 Expand() 会在 TransformPoint 上抛 NullReference，
        // 表现就是"点了 setting_icon 完全没反应"（连后面的淡入都跑不到）。
        Vector2 localPoint = openedUI.anchoredPosition;
        if (closedUI != null && canvasRect != null)
        {
            Vector3 worldPos = closedUI.TransformPoint(closedUI.rect.center);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, worldPos, null, out localPoint);
        }

        openedUI.gameObject.SetActive(true);
        openedUI.anchoredPosition = localPoint;

        Vector2 openedSize = openedUI.sizeDelta;
        var scaleFrom = Vector3.one;
        if (closedSize.x > 0.01f && closedSize.y > 0.01f && openedSize.x > 0.01f && openedSize.y > 0.01f)
        {
            scaleFrom = new Vector3(closedSize.x / openedSize.x, closedSize.y / openedSize.y, 1f);
        }

        openedUI.localScale = scaleFrom;
        openedUIGroup.alpha = 0f;

        // 展开 = 可以点：收起时会把 blocksRaycasts 关掉（否则看不见的窗口会挡住下面的 UI），
        // 不在这里恢复的话，窗口看着正常、里面的按钮却一个都点不动。
        openedUIGroup.blocksRaycasts = true;
        openedUIGroup.interactable = true;

        openedUI.DOAnchorPos(savedOpenedPos, animationDuration);
        openedUI.DOScale(Vector3.one, animationDuration);
        openedUIGroup.DOFade(1f, animationDuration);
    }

    public void Collapse()
    {
        KillTweens();
        savedOpenedPos = openedUI.anchoredPosition;
        savedOpenedScale = openedUI.localScale;

        // 和 Expand 同理：closedUI 为空时不要碰它（否则收起也会抛 NullReference）。
        if (closedUI != null && canvasRect != null)
        {
            Vector3 worldPos = closedUI.TransformPoint(closedUI.rect.center);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, worldPos, null, out var localPoint);

            Vector2 openedSize = openedUI.sizeDelta;
            var scaleTo = Vector3.one;
            if (closedSize.x > 0.01f && closedSize.y > 0.01f && openedSize.x > 0.01f && openedSize.y > 0.01f)
            {
                scaleTo = new Vector3(closedSize.x / openedSize.x, closedSize.y / openedSize.y, 1f);
            }

            openedUI.DOAnchorPos(localPoint, animationDuration);
            openedUI.DOScale(scaleTo, animationDuration);
        }
        // 收起 = 不吃点击：不然这个已经看不见（甚至已 SetActive(false)）的窗口还会拦着玩法 UI。
        openedUIGroup.blocksRaycasts = false;
        openedUIGroup.interactable = false;

        openedUIGroup.DOFade(0f, animationDuration).OnComplete(() =>
        {
            openedUI.gameObject.SetActive(false);
        });
    }
}
