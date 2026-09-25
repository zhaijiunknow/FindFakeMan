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
    /// 小软件「笔记」页：本局的观测记录 + 结论选项。
    ///
    /// 两段式，为了"控件可以烘进场景" ✓：
    /// - <see cref="Build"/>（public ✓）：**建控件**。运行时（`buildAtRuntime`）或建造工具在编辑期调它 ✓；
    /// - <see cref="WireEvents"/>：**接点击**。`onClick.AddListener` 在编辑期不会被序列化 ✗，
    ///   所以无论控件是烘的还是运行时建的 ✓，接线都统一在 Awake 里做一次 ✓（用固定方法 ✓，能 Remove ✓）。
    /// </summary>
    public sealed class CaseJournalView : MonoBehaviour
    {
        [Header("行为")]
        [Tooltip("至少读到几条观测才给结论按钮。")]
        [SerializeField] private int minReadings = 1;
        [Tooltip("自己搭版式。建造工具烘场景时会设成 false ✓（控件已经在场景里了）。")]
        [SerializeField] private bool buildAtRuntime = true;

        // 旧字段：以前笔记挂在窗口根上、由建造工具指定"内容容器" ✗。
        // 现在版式铺在自己这页上 ✓，保留只是为了让建造工具的 FindProperty 不会拿到 null ✗→✓。
        [SerializeField] private RectTransform contentRoot;

        [Header("控件引用（建造工具烘场景时写入 ✓）")]
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private Button fakeHumanButton;
        [SerializeField] private Button humanButton;
        [SerializeField] private Button backButton;

        [Header("结算（提交结论之后）")]
        [Tooltip("「结束调查」要去的场景（默认回片头）。")]
        [SerializeField] private string exitSceneName = "OpeningCinematic";

        private CaseDirector director;
        private int lastReadings = -1;
        private int lastAnomalies = -1;
        private bool lastSubmitted;
        private bool resultWired;
        private bool wired;

        private void Awake()
        {
            if (buildAtRuntime)
            {
                Build();
            }

            WireEvents();
        }

        private void Update()
        {
            if (director == null && !Services.TryGet<CaseDirector>(out director))
            {
                return;
            }

            RefreshBody();
            RefreshButtons();
        }

        /// <summary>建控件（运行时或建造工具在编辑期调用 ✓）。**不接点击** ✗ —— 那是 WireEvents 的事 ✓。</summary>
        public void Build()
        {
            var font = SmallAppPageStyle.ResolveFont(this);

            SmallAppPageStyle.Title(transform, font, "笔记");
            bodyText = SmallAppPageStyle.Body(transform, font, string.Empty);
            bodyText.rectTransform.anchorMin = new Vector2(0.05f, 0.34f);
            bodyText.rectTransform.anchorMax = new Vector2(0.95f, 0.84f);

            // 两格居中 ✓ —— 原来还有第三格「JournalHint」提示板 ✗，已删 ✓：
            // 它显示的那条"还差几条异常"正文（`BuildJournalText` 开头 ✓）本来就有 ✓，
            // 提交后又和「── 结论 ──」段重复 ✗；删掉后两格改居中 ✓（`0.08–0.48` / `0.52–0.92` ✓）。
            // 场景里是**直接改 YAML 删的** ✓（没让你重建 ✓），这里同步删是为了下次重建时不复活 ✗。
            fakeHumanButton = SmallAppPageStyle.Choice(transform, "Choice_FakeHuman", "是伪人", font, 0.08f, 0.48f, 0.21f, 0.31f);
            humanButton = SmallAppPageStyle.Choice(transform, "Choice_Human", "是正常人", font, 0.52f, 0.92f, 0.21f, 0.31f);

            backButton = SmallAppPageStyle.BackButton(transform, font, null);

            fakeHumanButton.gameObject.SetActive(false);
            humanButton.gameObject.SetActive(false);
        }

        /// <summary>接点击（幂等 ✓）。为什么不用 lambda：lambda 没法 RemoveListener ✗。</summary>
        public void WireEvents()
        {
            if (wired)
            {
                return;
            }

            wired = true;

            if (fakeHumanButton != null)
            {
                fakeHumanButton.onClick.RemoveListener(OnClickFakeHuman);
                fakeHumanButton.onClick.AddListener(OnClickFakeHuman);
            }

            if (humanButton != null)
            {
                humanButton.onClick.RemoveListener(OnClickHuman);
                humanButton.onClick.AddListener(OnClickHuman);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(BackToMenu);
                backButton.onClick.AddListener(BackToMenu);
            }
        }

        private void OnClickFakeHuman() => Submit(true);

        private void OnClickHuman() => Submit(false);

        private void RefreshBody()
        {
            if (bodyText == null)
            {
                return;
            }

            // 提交结论也要重刷 ✓：结算结果现在长在笔记正文里（原来那屏全屏结算板已经不要了 ✗）。
            // 少了这一条，"下完结论正文不更新"✗ —— 因为读数没变，只有 submitted 变了 ✓。
            if (director.ReadingCount == lastReadings
                && director.FoundAnomalies == lastAnomalies
                && director.HasSubmitted == lastSubmitted)
            {
                return;
            }

            lastReadings = director.ReadingCount;
            lastAnomalies = director.FoundAnomalies;
            lastSubmitted = director.HasSubmitted;
            bodyText.text = director.BuildJournalText();
        }

        private void RefreshButtons()
        {
            if (fakeHumanButton == null || humanButton == null)
            {
                return;
            }

            // 提交之后：这两格不再是「下结论」，而是收尾 ✓ —— 改成「再调查一次 / 结束调查」。
            // 为什么复用它们：位置尺寸都已经烘进场景了 ✓，而且玩家此刻手就在这一页上 ✓，
            // 不用再盖一层全屏结算板 ✗（原来的 CaseResult 就是这么干的，现在不要了 ✗）。
            if (director.HasSubmitted)
            {
                ShowResultButtons();
                return;
            }

            // 提示不再单独占一格 ✗→✓（「还差几条异常」正文第一行就有 ✓，判定结果在「── 结论 ──」段里 ✓）。
            var show = director.HasCase && director.ReadingCount >= minReadings;
            fakeHumanButton.gameObject.SetActive(show);
            humanButton.gameObject.SetActive(show);
        }

        /// <summary>把两个结论格换成收尾按钮（幂等 ✓）。标签改它们自己的 TMP 子节点，不重建控件 ✓。</summary>
        private void ShowResultButtons()
        {
            if (!resultWired)
            {
                resultWired = true;

                // 先把「下结论」的监听摘掉 ✗ —— 不然提交完再点一下，等于又下一遍结论（虽然会被 hasSubmitted 挡住 ✓，但没必要留着）。
                fakeHumanButton.onClick.RemoveListener(OnClickFakeHuman);
                humanButton.onClick.RemoveListener(OnClickHuman);
                fakeHumanButton.onClick.AddListener(OnClickRestart);
                humanButton.onClick.AddListener(OnClickExit);

                SetButtonLabel(fakeHumanButton, "再调查一次");
                SetButtonLabel(humanButton, "结束调查");
            }

            fakeHumanButton.gameObject.SetActive(true);
            humanButton.gameObject.SetActive(true);
        }

        private static void SetButtonLabel(Button button, string content)
        {
            var label = button != null ? button.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (label != null)
            {
                label.text = content;
            }
        }

        /// <summary>
        /// 再调查一次：重开当前场景。
        /// 注意这**不是**"同一局重来" ✗ —— 本局种子是进关卡时摇的（序幕交接单只在那一次生效 ✓），
        /// 重开等于新摇一颗种子，也就是**换一个人** ✓。想要固定案情就把 CaseDirector 的固定种子打开 ✓。
        /// </summary>
        private void OnClickRestart()
        {
            Load(SceneManager.GetActiveScene().name);
        }

        /// <summary>结束调查：回片头（和原来那屏结算板的「结束调查」完全同义 ✓）。</summary>
        private void OnClickExit()
        {
            Load(exitSceneName);
        }

        private static void Load(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[Journal] 想切场景，但场景名是空的。");
                return;
            }

            if (Services.TryGet<SceneFlowManager>(out var sceneFlow))
            {
                sceneFlow.LoadSceneAsync(sceneName, SceneTransitionStyle.FullScreenCrt);
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        private void Submit(bool saidFakeHuman)
        {
            if (director == null)
            {
                return;
            }

            director.SubmitVerdict(saidFakeHuman);
        }

        private void BackToMenu()
        {
            var host = GetComponentInParent<SmallAppPageHost>(true);
            if (host != null)
            {
                host.BackToMenu();
            }
        }
    }
}
