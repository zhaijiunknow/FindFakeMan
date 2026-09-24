using Project.Core.Runtime.Framework;
using Project.Gameplay.Scripts.Case;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 「下结论」选项：在 `nothink`（详情区）里出现 **伪人 / 正常人** 两个选择，
    /// 点一下就把结论交给 <see cref="CaseDirector"/>，由它比对本局身份并结算胜负。
    ///
    /// 为什么不放在别处：详情区本来就是玩家"盯着一个东西看"的地方，
    /// 结论也是"看够了之后说一句话"，放这里最顺。
    ///
    /// 出现时机：已经读到至少 <see cref="minReadings"/> 条观测（默认 1）——
    /// 手上有读数了才给结论入口，免得一进关就乱点。
    /// **读数不够也允许交**（那是玩家的赌），提示行会告诉他"异常 1/2"。
    ///
    /// 引用由建造工具接（`BigApp/nothink/VerdictRow` 下两个按钮 + 提示行）；
    /// 不直接连 CaseDirector 的引用，而是走 Services 查 —— 场景里少接一根线。
    /// </summary>
    public sealed class VerdictPanelView : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("整行（含两个按钮与提示）；留空就用自己。")]
        [SerializeField] private GameObject root;
        [Tooltip("「是伪人」按钮。")]
        [SerializeField] private Button fakeHumanButton;
        [Tooltip("「是正常人」按钮。")]
        [SerializeField] private Button humanButton;
        [Tooltip("提示行（可选）：显示已经凑到几条异常。")]
        [SerializeField] private TextMeshProUGUI hintText;
        [Tooltip("字体来源（留空就从本面板里已有的中文文本借字体）。")]
        [SerializeField] private TextMeshProUGUI fontSource;

        [Header("行为")]
        [Tooltip("读到几条观测之后才出现结论入口。")]
        [SerializeField] private int minReadings = 1;
        [Tooltip("没接线时自己把这一行建出来（不依赖建造工具连线）。")]
        [SerializeField] private bool buildRowAtRuntime = true;

        private CaseDirector director;
        private bool visible;
        private bool resultShown;

        private void Awake()
        {
            // 没接线就自己在详情区里搭一行：详情区本来就有三行中文文本，字体直接借它们的，
            // 所以不需要场景里预先摆按钮、也不怕以后换预制体。
            if (buildRowAtRuntime && (fakeHumanButton == null || humanButton == null))
            {
                BuildRuntimeRow();
            }

            // 注意：**不能**把 root 默认成 gameObject —— 那样 SetVisible(false) 会把详情区整个关掉。
            // root 为 null 时走"只开关按钮/提示行"那条路（见 SetVisible）。

            if (fakeHumanButton != null)
            {
                fakeHumanButton.onClick.AddListener(() => Submit(true));
            }

            if (humanButton != null)
            {
                humanButton.onClick.AddListener(() => Submit(false));
            }

            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (fakeHumanButton != null)
            {
                fakeHumanButton.onClick.RemoveAllListeners();
            }

            if (humanButton != null)
            {
                humanButton.onClick.RemoveAllListeners();
            }
        }

        private void Update()
        {
            if (director == null && !Services.TryGet<CaseDirector>(out director))
            {
                SetVisible(false);
                return;
            }

            // 已交结论 = 结算：两个选项收起来，把结论留在这个位置。
            if (director.HasSubmitted)
            {
                SetVisible(true);
                if (!resultShown)
                {
                    resultShown = true;
                    ShowResult(director.LastResultText);
                }

                return;
            }

            var show = director.HasCase && director.ReadingCount >= minReadings;
            SetVisible(show);

            if (show && hintText != null)
            {
                hintText.text = director.IsEnoughToConclude
                    ? $"异常读数 {director.FoundAnomalies} 条 —— 可以下结论了。"
                    : $"异常读数 {director.FoundAnomalies}/{director.CorroborationNeeded}（不够也能交，但要赌）";
            }
        }

        /// <summary>结算态：按钮收掉，结论铺满这一行（真正的状态切换由 CaseDirector 做）。</summary>
        private void ShowResult(string text)
        {
            if (fakeHumanButton != null)
            {
                fakeHumanButton.gameObject.SetActive(false);
            }

            if (humanButton != null)
            {
                humanButton.gameObject.SetActive(false);
            }

            if (hintText == null)
            {
                return;
            }

            hintText.text = string.IsNullOrEmpty(text) ? "结论已提交。" : text;
            hintText.fontSize = 20;

            var rect = hintText.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void Submit(bool saidFakeHuman)
        {
            if (director == null)
            {
                return;
            }

            director.SubmitVerdict(saidFakeHuman);
            SetVisible(false);
        }

        private void SetVisible(bool value)
        {
            if (visible == value)
            {
                return;
            }

            visible = value;

            if (root != null)
            {
                root.SetActive(value);
                return;
            }

            // 没有独立容器时只开关按钮与提示行 —— 绝不对自己整个 SetActive(false)，
            // 否则等于把详情区（组件所在的面板）关掉。
            if (fakeHumanButton != null && !resultShown)
            {
                fakeHumanButton.gameObject.SetActive(value);
            }

            if (humanButton != null && !resultShown)
            {
                humanButton.gameObject.SetActive(value);
            }

            if (hintText != null)
            {
                hintText.gameObject.SetActive(value);
            }
        }

        // ---------- 运行时自己搭那一行（不依赖预制体/场景里预先摆好按钮）----------

        private void BuildRuntimeRow()
        {
            var font = ResolveFont();

            var rowGo = new GameObject("VerdictRow", typeof(RectTransform));
            rowGo.transform.SetParent(transform, false);
            var rowRect = (RectTransform)rowGo.transform;
            rowRect.anchorMin = new Vector2(0.03f, 0.03f);
            rowRect.anchorMax = new Vector2(0.97f, 0.30f);
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;

            root = rowGo;
            fakeHumanButton = CreateChoice(rowRect, "Choice_FakeHuman", "是伪人", font, 0f, 0.31f);
            humanButton = CreateChoice(rowRect, "Choice_Human", "是正常人", font, 0.33f, 0.64f);
            hintText = CreateLabel(rowRect, "VerdictHint", string.Empty, font, 0.66f, 1f, 18,
                new Color(0.82f, 0.93f, 1f, 0.85f));

            Debug.Log("[Case] 结论选项已在详情区里自动搭好（是伪人 / 是正常人）。");
        }

        private TMP_FontAsset ResolveFont()
        {
            if (fontSource != null && fontSource.font != null)
            {
                return fontSource.font;
            }

            // 详情区里本来就有 Name / Desc / Status 三行中文：借它们的字体，中文才不会变豆腐块。
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
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

        private static TextMeshProUGUI CreateLabel(RectTransform parent, string name, string label, TMP_FontAsset font, float xMin, float xMax, float fontSize, Color color)
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
            // 标签绝不能吃射线，否则按钮点不动。
            text.raycastTarget = false;
            return text;
        }
    }
}
