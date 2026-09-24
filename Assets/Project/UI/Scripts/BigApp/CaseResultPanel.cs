using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Case;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 结算界面：玩家在详情区交完结论之后，盖住整个窗口给一屏结果
    /// —— 判定对不对、真相是什么、这局读到几条异常、种子多少（方便复现），以及「再调查一次 / 结束调查」。
    ///
    /// 为什么自己搭 UI：它是"一局结束才出现"的东西，放在预制体里会让那个窗口一直背着一块平时看不见的板子；
    /// 自己搭还能顺手复用场景里已有的中文 TMP 字体（不用再配一遍字体资产）。
    ///
    /// 显示时机由 <see cref="CaseDirector.HasSubmitted"/> 决定，跟结论选项（VerdictPanelView）是一对：
    /// 那边负责"下结论"，这边负责"看法结果并决定下一步"。
    /// </summary>
    public sealed class CaseResultPanel : MonoBehaviour
    {
        [Header("行为")]
        [Tooltip("「结束调查」要去的场景（默认回片头）。")]
        [SerializeField] private string exitSceneName = "OpeningCinematic";
        [Tooltip("「再调查一次」要重开的场景；留空 = 重开当前场景。")]
        [SerializeField] private string restartSceneName = string.Empty;
        [Tooltip("显示时是否吞掉点击（挡住底下的窗口）。")]
        [SerializeField] private bool blockInput = true;

        private CaseDirector director;
        private GameObject cover;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI verdictText;
        private TextMeshProUGUI truthText;
        private TextMeshProUGUI readingText;
        private TextMeshProUGUI seedText;
        private bool built;
        private bool shown;

        private void Update()
        {
            if (director == null && !Services.TryGet<CaseDirector>(out director))
            {
                return;
            }

            if (!director.HasSubmitted)
            {
                return;
            }

            if (!built)
            {
                Build();
            }

            if (shown)
            {
                return;
            }

            shown = true;
            Fill(director);
            if (cover != null)
            {
                cover.SetActive(true);
            }
        }

        // ---------- 搭界面 ----------

        private void Build()
        {
            built = true;
            var font = ResolveFont();

            cover = new GameObject("CaseResultCover", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cover.transform.SetParent(transform, false);
            Stretch((RectTransform)cover.transform);

            var coverImage = cover.GetComponent<Image>();
            coverImage.color = new Color(0f, 0f, 0f, 0.78f);
            coverImage.raycastTarget = blockInput;

            // 一块居中的板
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(cover.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(760f, 460f);
            panel.GetComponent<Image>().color = new Color(0.06f, 0.09f, 0.13f, 0.98f);

            titleText = Label(panelRect, "Title", "调查结束", font, 0.06f, 0.84f, 0.94f, 40, new Color(0.85f, 0.94f, 1f));
            verdictText = Label(panelRect, "Verdict", string.Empty, font, 0.08f, 0.62f, 0.82f, 30, Color.white);
            truthText = Label(panelRect, "Truth", string.Empty, font, 0.08f, 0.46f, 0.66f, 26, new Color(0.9f, 0.9f, 0.9f));
            readingText = Label(panelRect, "Readings", string.Empty, font, 0.08f, 0.30f, 0.50f, 24, new Color(0.8f, 0.86f, 0.95f));
            seedText = Label(panelRect, "Seed", string.Empty, font, 0.08f, 0.16f, 0.36f, 20, new Color(0.7f, 0.78f, 0.88f));

            Choice(panelRect, "Restart", "再调查一次", font, 0.10f, 0.46f, 0.07f, 0.15f, ReloadScene);
            Choice(panelRect, "Exit", "结束调查", font, 0.54f, 0.90f, 0.07f, 0.15f, ExitScene);

            cover.SetActive(false);
            Debug.Log("[Case] 结算界面已就绪（提交结论后出现）。");
        }

        private void Fill(CaseDirector caseDirector)
        {
            var correct = caseDirector.LastVerdictCorrect;

            if (titleText != null)
            {
                titleText.text = correct ? "调查结束 —— 判断正确" : "调查结束 —— 判断错误";
            }

            if (verdictText != null)
            {
                verdictText.text = string.IsNullOrEmpty(caseDirector.LastResultText)
                    ? string.Empty
                    : caseDirector.LastResultText;
            }

            if (truthText != null)
            {
                truthText.text = $"真相：目标其实是{(caseDirector.IsFakeHuman ? "伪人" : "普通人")}。";
            }

            if (readingText != null)
            {
                readingText.text = $"读数：异常 {caseDirector.FoundAnomalies} 条"
                                   + $"（判定需要 {caseDirector.CorroborationNeeded} 条），"
                                   + $"一共读过 {caseDirector.ReadingCount} 件家具。";
            }

            if (seedText != null)
            {
                seedText.text = $"本局种子 {caseDirector.Seed}（同一颗种子会摇出同一份案情）";
            }
        }

        // ---------- 按钮 ----------

        private void ReloadScene()
        {
            var sceneName = string.IsNullOrEmpty(restartSceneName)
                ? SceneManager.GetActiveScene().name
                : restartSceneName;

            Load(sceneName);
        }

        private void ExitScene()
        {
            Load(exitSceneName);
        }

        private static void Load(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[Case] 结算界面想切场景，但场景名是空的。");
                return;
            }

            if (Services.TryGet<SceneFlowManager>(out var sceneFlow))
            {
                sceneFlow.LoadSceneAsync(sceneName, SceneTransitionStyle.FullScreenCrt);
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        // ---------- 小工具 ----------

        private TMP_FontAsset ResolveFont()
        {
            // 场景里已经有中文 TMP 文本（窗口里的那些）：借它们的字体，中文才不会变豆腐块。
            var texts = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var text in texts)
            {
                if (text != null && text.font != null)
                {
                    return text.font;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI Label(RectTransform parent, string name, string content, TMP_FontAsset font,
            float xMin, float xMax, float yMax, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(xMin, yMax - 0.13f);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }

            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;
            return text;
        }

        private void Choice(RectTransform parent, string name, string label, TMP_FontAsset font,
            float xMin, float xMax, float yMin, float yMax, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.16f, 0.24f, 0.34f, 1f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var text = Label(rect, "Label", label, font, 0f, 1f, 1f, 24, Color.white);
            text.alignment = TextAlignmentOptions.Center;

            var labelRect = (RectTransform)text.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }
    }
}
