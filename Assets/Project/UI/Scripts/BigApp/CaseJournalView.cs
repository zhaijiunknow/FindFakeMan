using Project.Core.Runtime.Framework;
using Project.Gameplay.Scripts.Case;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// SmallApp 里的「笔记」页：把这一局的观测记录和结论选项放在小软件窗口里（恐怖症那种笔记本）。
    ///
    /// 为什么要有它：`nothink` 是"盯着单件东西看"的详情区，把"证据进度 + 下结论"塞在那里很别扭 ✗；
    /// 小软件本来就是个多页窗口（预制体里已有 `异常相册`/`道具`/`设置` 等分页 ✓），
    /// 笔记自然是其中一页 ✓ —— 打开窗口看着记录想清楚，再勾结论 ✓。
    ///
    /// 数据全部来自 <see cref="CaseDirector"/>（本类只显示、不判断）：
    /// - 正文 = <c>BuildJournalText()</c>（案号 / 种子 / `异常 n/m` / 每条观测）✓
    /// - 结论按钮 = `SubmitVerdict(true/false)` ✓
    ///
    /// 版式：可以连 <see cref="bodyText"/> 与 <see cref="buttonRow"/>（预制体里已有的文本/按钮行 ✓），
    /// 留空就自己搭（正文铺在窗口的右侧内容区，两个按钮在底部 ✓）—— 和结论选项一样走运行时自建，
    /// 所以**不需要改 `SmallApp.prefab`** ✗（那个预制体被 OpeningCinematic 的 PanelManager 用着 ✓）。
    /// </summary>
    public sealed class CaseJournalView : MonoBehaviour
    {
        [Header("引用（留空则自己搭）")]
        [Tooltip("正文文本（观测列表）。")]
        [SerializeField] private TextMeshProUGUI bodyText;
        [Tooltip("结论按钮那一行的容器。")]
        [SerializeField] private RectTransform buttonRow;
        [Tooltip("自己搭版式时，正文放哪（默认放在窗口右侧的内容区）。")]
        [SerializeField] private RectTransform contentRoot;

        [Header("行为")]
        [Tooltip("至少要读到几条观测，才给结论按钮（和原来的详情区口径一致）。")]
        [SerializeField] private int minReadings = 1;
        [Tooltip("没接线时自己把正文与按钮搭出来。")]
        [SerializeField] private bool buildAtRuntime = true;

        private CaseDirector director;
        private Button fakeHumanButton;
        private Button humanButton;
        private TextMeshProUGUI hintText;
        private int lastReadingCount = -1;
        private int lastAnomalyCount = -1;
        private bool resultShown;

        private void Awake()
        {
            if (buildAtRuntime)
            {
                BuildIfNeeded();
            }
        }

        private void Update()
        {
            if (director == null && !Services.TryGet<CaseDirector>(out director))
            {
                return;
            }

            RefreshText();
            RefreshButtons();
        }

        // ---------- 刷新 ----------

        private void RefreshText()
        {
            if (director.ReadingCount == lastReadingCount && director.FoundAnomalies == lastAnomalyCount)
            {
                return;
            }

            lastReadingCount = director.ReadingCount;
            lastAnomalyCount = director.FoundAnomalies;

            if (bodyText != null)
            {
                bodyText.text = director.BuildJournalText();
            }
        }

        private void RefreshButtons()
        {
            if (buttonRow == null)
            {
                return;
            }

            var show = director.HasCase && !director.HasSubmitted && director.ReadingCount >= minReadings;

            if (fakeHumanButton != null)
            {
                fakeHumanButton.gameObject.SetActive(show);
            }

            if (humanButton != null)
            {
                humanButton.gameObject.SetActive(show);
            }

            if (show && hintText != null)
            {
                hintText.text = director.IsEnoughToConclude
                    ? "异常够互相印证了 —— 可以下结论。"
                    : $"异常 {director.FoundAnomalies}/{director.CorroborationNeeded}（不够也能交，但要赌）";
            }

            // 交完结论：按钮收掉，把一个"已提交"的说明留在原地（结算面板会盖上来）。
            if (director.HasSubmitted && !resultShown)
            {
                resultShown = true;
                if (hintText != null)
                {
                    hintText.text = "结论已提交。";
                }
            }
        }

        // ---------- 自建版式 ----------

        private void BuildIfNeeded()
        {
            var font = ResolveFont();

            // 正文：优先用连进来的；没连就在右侧内容区铺一块（避开左侧那个分页栏）。
            var host = contentRoot != null ? contentRoot : (RectTransform)transform;
            if (bodyText == null)
            {
                var textGo = new GameObject("JournalBody", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(host, false);

                var rect = (RectTransform)textGo.transform;
                if (contentRoot == null)
                {
                    // 默认版式：右侧 62% 宽、上下留边（左边那 26% 是预制体的分页栏，别压住它）。
                    rect.anchorMin = new Vector2(0.30f, 0.12f);
                    rect.anchorMax = new Vector2(0.96f, 0.94f);
                }
                else
                {
                    rect.anchorMin = new Vector2(0.04f, 0.16f);
                    rect.anchorMax = new Vector2(0.96f, 0.94f);
                }

                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                bodyText = textGo.GetComponent<TextMeshProUGUI>();
                if (font != null)
                {
                    bodyText.font = font;
                }

                bodyText.fontSize = 22;
                bodyText.color = new Color(0.88f, 0.94f, 1f, 1f);
                bodyText.alignment = TextAlignmentOptions.TopLeft;
                bodyText.raycastTarget = false;
            }

            if (buttonRow == null)
            {
                var rowGo = new GameObject("JournalChoices", typeof(RectTransform));
                rowGo.transform.SetParent(host, false);

                var rowRect = (RectTransform)rowGo.transform;
                if (contentRoot == null)
                {
                    rowRect.anchorMin = new Vector2(0.30f, 0.02f);
                    rowRect.anchorMax = new Vector2(0.96f, 0.11f);
                }
                else
                {
                    rowRect.anchorMin = new Vector2(0.04f, 0.02f);
                    rowRect.anchorMax = new Vector2(0.96f, 0.14f);
                }

                rowRect.offsetMin = Vector2.zero;
                rowRect.offsetMax = Vector2.zero;

                buttonRow = rowRect;
                fakeHumanButton = CreateChoice(rowRect, "Choice_FakeHuman", "是伪人", font, 0f, 0.30f);
                humanButton = CreateChoice(rowRect, "Choice_Human", "是正常人", font, 0.33f, 0.63f);
                hintText = CreateLabel(rowRect, "JournalHint", string.Empty, font, 0.66f, 1f, 18,
                    new Color(0.82f, 0.93f, 1f, 0.85f));

                fakeHumanButton.onClick.AddListener(() => Submit(true));
                humanButton.onClick.AddListener(() => Submit(false));

                fakeHumanButton.gameObject.SetActive(false);
                humanButton.gameObject.SetActive(false);
            }
        }

        private void Submit(bool saidFakeHuman)
        {
            if (director == null)
            {
                return;
            }

            director.SubmitVerdict(saidFakeHuman);
        }

        private TMP_FontAsset ResolveFont()
        {
            // 小软件里本来就有中文 TMP 文本：借它们的字体，中文才不会变豆腐块。
            var canvas = GetComponentInParent<Canvas>();
            var host = canvas != null ? canvas.transform : transform.root;
            var texts = host.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                if (text != null && text.font != null)
                {
                    return text.font;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static Button CreateChoice(RectTransform parent, string name, string label, TMP_FontAsset font, float xMin, float xMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(xMin, 0f);
            rect.anchorMax = new Vector2(xMax, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.13f, 0.18f, 0.26f, 0.95f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            CreateLabel(rect, "Label", label, font, 0f, 1f, 20, Color.white);
            return button;
        }

        private static TextMeshProUGUI CreateLabel(RectTransform parent, string name, string label, TMP_FontAsset font,
            float xMin, float xMax, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(xMin, 0f);
            rect.anchorMax = new Vector2(xMax, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }

            text.text = label;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }
    }
}
