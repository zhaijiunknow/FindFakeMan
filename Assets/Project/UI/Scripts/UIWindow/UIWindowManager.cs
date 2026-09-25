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

    [Header("按钮接线")]
    [Tooltip("是否自动接标题栏的 red / green / blue（关闭 / 铺满 / 还原）。\n"
             + "小软件那个窗口要**关掉** ✓ —— 它的三个键由 SmallAppPageHost 接\n"
             + "（口径：红=关闭、蓝=最小化、绿=全屏 ✓），不然两边都接会打架 ✗。")]
    [SerializeField] private bool bindWindowButtons = true;

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
        //
        // ⚠️ 可以整块关掉 ✓（bindWindowButtons = false）：小软件（SmallApp）就是这样 ✓ ——
        // 它的三个键由 SmallAppPageHost 接 ✗→✓，否则**两边都接** ✓：
        //   green 同时"铺满矩形 + 切系统全屏"✗、blue 同时"还原矩形 + 收起窗口"✗ —— 语义直接打架 ✓。
        // 口径（小软件）：红=关闭 ✓、蓝=最小化（保留当前页 ✓）、绿=全屏 ✓。
        if (bindWindowButtons)
        {
            BindButton("red", OnCloseWindow);
            BindButton("green", OnMaximize);
            BindButton("blue", OnRestore);
        }
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
        Vector3 worldPos = closedUI.TransformPoint(closedUI.rect.center);
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, worldPos, null, out localPoint);

        openedUI.gameObject.SetActive(true);

        // **必须把射线放回来** ✗→✓：收起时构建器把 `blocksRaycasts` 设成了 false ✓
        //（`BuildInvestigationScene` 里那句"缩在齿轮里点不到"✓），但**展开时没人把它打开** ✗ ——
        // 于是窗口看得见、里面所有按钮都点不动 ✗（CanvasGroup 一关，整棵子树都不吃射线 ✓）。
        openedUIGroup.blocksRaycasts = true;
        openedUIGroup.interactable = true;

        openedUI.anchoredPosition = localPoint;

        Vector2 openedSize = openedUI.sizeDelta;
        float scaleX = closedSize.x / openedSize.x;
        float scaleY = closedSize.y / openedSize.y;
        openedUI.localScale = new Vector3(scaleX, scaleY, 1f);
        openedUIGroup.alpha = 0f;

        openedUI.DOAnchorPos(savedOpenedPos, animationDuration);
        openedUI.DOScale(Vector3.one, animationDuration);
        openedUIGroup.DOFade(1f, animationDuration);
    }

    public void Collapse()
    {
        KillTweens();

        // 收起过程中先断掉交互 ✓（淡出那 0.4 秒里别让玩家点到正在缩小的按钮 ✓）。
        openedUIGroup.blocksRaycasts = false;
        openedUIGroup.interactable = false;

        savedOpenedPos = openedUI.anchoredPosition;
        savedOpenedScale = openedUI.localScale;

        Vector3 worldPos = closedUI.TransformPoint(closedUI.rect.center);
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, worldPos, null, out localPoint);

        Vector2 openedSize = openedUI.sizeDelta;
        float scaleX = closedSize.x / openedSize.x;
        float scaleY = closedSize.y / openedSize.y;

        openedUI.DOAnchorPos(localPoint, animationDuration);
        openedUI.DOScale(new Vector3(scaleX, scaleY, 1f), animationDuration);
        openedUIGroup.DOFade(0f, animationDuration).OnComplete(() =>
        {
            openedUI.gameObject.SetActive(false);
        });
    }
}
