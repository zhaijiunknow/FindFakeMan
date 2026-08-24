using System.Linq;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Narrative.Scripts;
using Project.UI.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Scripts
{
    /// <summary>
    /// Button 通用动作组件：在 Inspector 选择 ActionType 即可执行对应逻辑。
    /// 绑定 Button 引用后自动监听点击；也可在 Button 的 onClick 事件里手动调用 Execute()。
    /// </summary>
    public class ButtonAction : MonoBehaviour
    {
        public enum ActionType
        {
            ShowPanel,       // UIManager.ShowPanel(panelId) —— 需配合订阅 OnPanelShown 的面板系统
            HidePanel,       // UIManager.HidePanel(panelId)
            ShowVNPanel,     // UIManager.ShowVNPanel()
            HideVNPanel,     // UIManager.HideVNPanel()
            ShowGameObject,  // targetObject.SetActive(true)
            HideGameObject,  // targetObject.SetActive(false)
            LoadScene,       // SceneFlowManager.LoadSceneAsync(sceneName)
            OpenPanel,       // PanelManager.OpenPanelAsync(panelId) —— 面板栈打开（targetId 复用为 panelId）
            ClosePanel,      // PanelManager.ClosePanelByIdAsync(panelId)
            CloseTopPanel,      // PanelManager.CloseTopAsync()
            OpenWindow,         // 打开 SmallApp（主界面窗口）：SmallAppWindowController.OnOpen()
            HideSelfShowTarget, // 隐藏自身，显示 targetObject（如 start 显示 all_button）
            QuitGame,           // 退出游戏：编辑器退 Play 模式，打包后 Application.Quit()
            Start               // start 按钮：序章未完成播序章，完成后隐藏自身显示 targetObject(all_button)
        }

        [Header("按钮")]
        [Tooltip("留空则自动查找同物体上的 Button。若想通过 Inspector 的 onClick 手动调用 Execute()，可保持留空")]
        [SerializeField] private Button button;

        [Header("动作")]
        [SerializeField] private ActionType actionType;

        [Header("参数")]
        [Tooltip("ShowPanel/HidePanel 用面板 ID；LoadScene 用场景名")]
        [SerializeField] private string targetId;
        [Tooltip("ShowGameObject/HideGameObject 用。要显隐的目标物体")]
        [SerializeField] private GameObject targetObject;

        private void Reset()
        {
            if (button == null)
                button = GetComponent<Button>();
        }

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (button != null)
                button.onClick.AddListener(Execute);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(Execute);
        }

        /// <summary>
        /// 按所选枚举执行动作。可挂在 Button 的 onClick 上手动调用。
        /// </summary>
        public void Execute()
        {
            switch (actionType)
            {
                case ActionType.ShowPanel:
                    GetUIManager()?.ShowPanel(targetId);
                    break;
                case ActionType.HidePanel:
                    GetUIManager()?.HidePanel(targetId);
                    break;
                case ActionType.OpenPanel:
                    GetPanelManager()?.OpenPanelAsync(targetId).Forget();
                    break;
                case ActionType.ClosePanel:
                    GetPanelManager()?.ClosePanelByIdAsync(targetId).Forget();
                    break;
                case ActionType.CloseTopPanel:
                    GetPanelManager()?.CloseTopAsync().Forget();
                    break;
                case ActionType.OpenWindow:
                    FindObjectOfType<UIWindowManager>()?.Expand();
                    break;
                case ActionType.HideSelfShowTarget:
                    gameObject.SetActive(false);
                    if (targetObject != null) targetObject.SetActive(true);
                    break;
                case ActionType.QuitGame:
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
                case ActionType.Start:
                    StartFlowAsync().Forget();
                    break;
                case ActionType.ShowVNPanel:
                    GetUIManager()?.ShowVNPanel();
                    break;
                case ActionType.HideVNPanel:
                    GetUIManager()?.HideVNPanel();
                    break;
                case ActionType.ShowGameObject:
                    if (targetObject != null)
                        targetObject.SetActive(true);
                    else
                        Debug.LogWarning("ButtonAction: ShowGameObject 缺少目标物体");
                    break;
                case ActionType.HideGameObject:
                    if (targetObject != null)
                        targetObject.SetActive(false);
                    else
                        Debug.LogWarning("ButtonAction: HideGameObject 缺少目标物体");
                    break;
                case ActionType.LoadScene:
                    LoadSceneAsync(targetId).Forget();
                    break;
                default:
                    Debug.LogWarning($"ButtonAction: 未处理的动作类型 {actionType}");
                    break;
            }
        }

        private UIManager GetUIManager() =>
            Services.TryGet<UIManager>(out var ui) ? ui : null;

        private PanelManager GetPanelManager() =>
            Services.TryGet<PanelManager>(out var pm) ? pm : null;

        /// <summary>
        /// start 按钮流程：序章已完成 → 隐藏自身显示 targetObject(all_button)；
        /// 未完成 → 播放序章(VNDirector.StartChapter) + 标记完成，再隐藏自身显示 targetObject。
        /// </summary>
        private async UniTask StartFlowAsync()
        {
            if (Services.TryGet<FlagManager>(out var flags) && flags.Get("prologue_complete"))
            {
                HideSelfShow();
                return;
            }

            var director = FindObjectOfType<VNDirector>();
            var prologue = LoadChapter("chapter_prologue");
            if (director != null && prologue != null)
            {
                await director.StartChapter(prologue);
                if (Services.TryGet<FlagManager>(out var f))
                {
                    f.Set("prologue_complete");
                }
            }
            else
            {
                Debug.LogWarning("ButtonAction: 未找到 VNDirector 或序章章节，无法播放序章。");
            }

            HideSelfShow();
        }

        private void HideSelfShow()
        {
            gameObject.SetActive(false);
            if (targetObject != null)
            {
                targetObject.SetActive(true);
            }
        }

        private static VNChapterConfig LoadChapter(string chapterId) =>
            Resources.LoadAll<VNChapterConfig>(string.Empty)
                .FirstOrDefault(c => c != null && c.ChapterId == chapterId);

        private async UniTask LoadSceneAsync(string sceneName)
        {
            if (Services.TryGet<SceneFlowManager>(out var sceneFlow))
            {
                await sceneFlow.LoadSceneAsync(sceneName);
            }
            else
            {
                Debug.LogWarning("ButtonAction: 未找到 SceneFlowManager，无法加载场景");
            }
        }
    }
}
