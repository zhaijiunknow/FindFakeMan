using System;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Core.Runtime.Managers
{
    /// <summary>切场景时的过场样式。</summary>
    public enum SceneTransitionStyle
    {
        /// <summary>全屏 CRT 关机/开机（默认）：整台机器换环境的观感。</summary>
        FullScreenCrt = 0,

        /// <summary>只在 UI 板块（mainUI 的 game）里播：同一个软件窗口换视图的观感。</summary>
        PanelLocal = 1,

        /// <summary>不做任何过场（硬切）。</summary>
        None = 2,
    }

    /// <summary>
    /// 场景流转。**所有**切场景都应该走这里（ButtonAction / MainMenuController / VN 的 EndAction.LoadScene）。
    ///
    /// 过场：
    ///  - <see cref="SceneTransitionStyle.FullScreenCrt"/>（默认）用 <see cref="ScreenTransition"/>：全屏 CRT 关机 → 加载 → 开机；
    ///  - <see cref="SceneTransitionStyle.PanelLocal"/> 用 <see cref="PanelTransition"/>：只在 mainUI 的 `game` 板块里播，
    ///    窗口边框/右栏/底栏/左栏全程不动（序幕 → 别墅走这条）；
    ///  - 关机在旧场景的板块里播，开机在新场景的板块里播 —— ExpandAsync 会先同步把新场景那块盖黑再动画，
    ///    所以不会闪出"加载完成那一帧"。
    /// </summary>
    public sealed class SceneFlowManager : ManagerBehaviour
    {
        [Header("转场")]
        [Tooltip("切场景时是否走过场。关掉就是硬切。")]
        [SerializeField] private bool useTransition = true;
        [Tooltip("出场（关机/合拢）时长，秒，不受 timeScale 影响。")]
        [SerializeField] private float collapseDuration = 0.8f;
        [Tooltip("入场（开机/展开）时长，秒。")]
        [SerializeField] private float expandDuration = 1f;

        /// <summary>是否正在切换场景（用来防重复触发）。</summary>
        public bool IsLoading { get; private set; }

        /// <summary>默认走全屏 CRT 过场。</summary>
        public UniTask LoadSceneAsync(string sceneName)
        {
            return LoadSceneAsync(sceneName, SceneTransitionStyle.FullScreenCrt);
        }

        public async UniTask LoadSceneAsync(string sceneName, SceneTransitionStyle style)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            if (IsLoading)
            {
                Debug.LogWarning($"[SceneFlow] 已在切换场景中，忽略重复请求：{sceneName}");
                return;
            }

            IsLoading = true;
            try
            {
                if (UseTransition(style))
                {
                    await CollapseAsync(style);
                }

                var operation = SceneManager.LoadSceneAsync(sceneName);
                if (operation == null)
                {
                    // 常见原因：场景没加进 Build Settings，或者名字拼错。
                    Debug.LogError($"[SceneFlow] 加载场景失败：{sceneName}（检查是否已加入 Build Settings）");
                    return;
                }

                await operation.ToUniTask();
            }
            catch (Exception exception)
            {
                // 别让异常把画面永远留在黑屏上。
                Debug.LogError($"[SceneFlow] 切换场景 {sceneName} 时出错：{exception}");
            }
            finally
            {
                try
                {
                    if (UseTransition(style))
                    {
                        // 不管成功失败都把画面收回来（成功时就是新场景的入场过场）。
                        await ExpandAsync(style);
                    }
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        private bool UseTransition(SceneTransitionStyle style)
        {
            return useTransition && style != SceneTransitionStyle.None;
        }

        private UniTask CollapseAsync(SceneTransitionStyle style)
        {
            return style == SceneTransitionStyle.PanelLocal
                ? PanelTransition.CollapseAsync(collapseDuration)
                : ScreenTransition.CollapseAsync(collapseDuration);
        }

        private UniTask ExpandAsync(SceneTransitionStyle style)
        {
            return style == SceneTransitionStyle.PanelLocal
                ? PanelTransition.ExpandAsync(expandDuration)
                : ScreenTransition.ExpandAsync(expandDuration);
        }
    }
}
