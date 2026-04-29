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

        [SerializeField] private string gameplaySceneName = "Stage2_Breach_Sample";
        [SerializeField] private Button continueButton;
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private GameObject rootButtonContainer;
        [SerializeField] private GameObject settingsPanel;
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
            ShowOnly(settingsPanel);
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

            await sceneFlowManager.LoadSceneAsync(sceneName);
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
            SetActive(settingsPanel, target == settingsPanel);
            SetActive(levelSelectPanel, target == levelSelectPanel);
            SetActive(creditsPanel, target == creditsPanel);
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
