using System.Linq;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.MainMenu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        private readonly struct Resolution
        {
            public Resolution(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public int Width { get; }
            public int Height { get; }
        }

        [SerializeField] private string gameplaySceneName = "Px2050_Villa";
        [SerializeField] private Button continueButton;
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private GameObject rootButtonContainer;
        [SerializeField] private UIWindowManager settingsPanel;
        [SerializeField] private GameObject levelSelectPanel;
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private GameObject[] settingTargets;

        private bool isBusy;
        private readonly Resolution[] presetResolutions =
        {
            new(1280, 720),
            new(1600, 900),
            new(1920, 1080)
        };

        private void Awake()
        {
            if (Services.TryGet<GameManager>(out var gameManager))
            {
                gameManager.SwitchState(GameState.Title);
            }

            RefreshContinueButton();
            ShowRoot();
        }

        public void OnNewGameClicked()
        {
            StartNewGameAsync().Forget();
        }

        public void OnContinueClicked()
        {
            ContinueGameAsync().Forget();
        }

        public void OnQuitClicked()
        {
#if UNITY_EDITOR
            Debug.Log("Quit requested from main menu.");
#else
            Application.Quit();
#endif
        }

        public void OnOpenSettings()
        {
            Expand(settingsPanel);
        }

        public void OnOpenLevelSelect()
        {
            ShowOnly(levelSelectPanel);
        }

        public void OnOpenCredits()
        {
            ShowOnly(creditsPanel);
        }

        public void OnToggleSettingTarget(int index)
        {
            if (settingTargets == null || index < 0 || index >= settingTargets.Length)
            {
                return;
            }

            var target = settingTargets[index];
            if (target != null)
            {
                target.SetActive(!target.activeSelf);
            }
        }

        public void OnSetResolution(int presetIndex)
        {
            if (presetIndex < 0 || presetIndex >= presetResolutions.Length)
            {
                return;
            }

            var resolution = presetResolutions[presetIndex];
            Screen.SetResolution(resolution.Width, resolution.Height, Screen.fullScreen);
        }

        public void OnToggleFullscreen()
        {
            Screen.fullScreen = !Screen.fullScreen;
        }

        public void OnToggleVSync()
        {
            QualitySettings.vSyncCount = QualitySettings.vSyncCount == 0 ? 1 : 0;
        }

        public void OnBackToMain()
        {
            ShowRoot();
        }

        public void OnSelectGameplayScene()
        {
            LoadSceneAsync(gameplaySceneName).Forget();
        }

        private async UniTaskVoid StartNewGameAsync()
        {
            if (isBusy)
            {
                return;
            }

            isBusy = true;
            try
            {
                if (!Services.TryGet<GameManager>(out var gameManager))
                {
                    Debug.LogWarning("MainMenuController could not find GameManager.");
                    return;
                }

                await gameManager.StartNewGame();
                await LoadSceneInternalAsync(gameplaySceneName);
            }
            finally
            {
                isBusy = false;
            }
        }

        private async UniTaskVoid ContinueGameAsync()
        {
            if (isBusy)
            {
                return;
            }

            if (!Services.TryGet<SaveManager>(out var saveManager) || !saveManager.HasAnySave())
            {
                RefreshContinueButton();
                return;
            }

            isBusy = true;
            try
            {
                if (!Services.TryGet<GameManager>(out var gameManager))
                {
                    Debug.LogWarning("MainMenuController could not find GameManager.");
                    return;
                }

                var slots = saveManager.GetAllSaveSlots();
                var latestSlot = slots.OrderByDescending(slot => slot.timestampTicks).FirstOrDefault();
                var latestSceneName = latestSlot?.sceneName;
                await gameManager.ContinueGame();
                await LoadSceneInternalAsync(string.IsNullOrWhiteSpace(latestSceneName) ? gameplaySceneName : latestSceneName);
            }
            finally
            {
                isBusy = false;
            }
        }

        private async UniTaskVoid LoadSceneAsync(string sceneName)
        {
            if (isBusy)
            {
                return;
            }

            isBusy = true;
            try
            {
                await LoadSceneInternalAsync(sceneName);
            }
            finally
            {
                isBusy = false;
            }
        }

        private async UniTask LoadSceneInternalAsync(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            if (!Services.TryGet<SceneFlowManager>(out var sceneFlowManager))
            {
                Debug.LogWarning("MainMenuController could not find SceneFlowManager.");
                return;
            }

            // 直接进**玩法关**时不走全屏 CRT ✗→✓。
            // 全屏 CRT 是"整屏黑掉 → 加载 → 再亮起来"（0.8s + 1.0s），而玩法关的窗口本身是铺满画布的，
            // 玩家从主菜单点「开始游戏/继续」时要看的就是那套 HUD 直接出现，中间再插一次全屏黑纯属倒退 ✗。
            // 玩法关的入场观感交给它自己：序幕那条路走的是「面板内 CRT」（只在 game 板块里收屏，见 ProloguePerformanceDirector ✓）。
            // 进别的场景（比如 Px2050_Prologue）仍然是全屏 CRT ✓。
            var style = sceneName == gameplaySceneName
                ? SceneTransitionStyle.None
                : SceneTransitionStyle.FullScreenCrt;

            await sceneFlowManager.LoadSceneAsync(sceneName, style);
        }

        private void RefreshContinueButton()
        {
            if (continueButton == null)
            {
                return;
            }

            continueButton.interactable = Services.TryGet<SaveManager>(out var saveManager) && saveManager.HasAnySave();
        }

        private void ShowRoot()
        {
            ShowOnly(rootPanel);
        }
        private void ShowOnly(GameObject target)
        {
            SetActive(rootPanel, target == rootPanel);
            SetActive(rootButtonContainer, target == rootPanel);
            //SetActive(settingsPanel, target == settingsPanel);
            SetActive(levelSelectPanel, target == levelSelectPanel);
            SetActive(creditsPanel, target == creditsPanel);
        }
        private static void Expand(UIWindowManager panel)
        {
            panel.Expand();
        }
        private static void SetActive(GameObject panel, bool visible)
        {
            if (panel != null)
            {
                panel.SetActive(visible);
            }
        }
    }
}
