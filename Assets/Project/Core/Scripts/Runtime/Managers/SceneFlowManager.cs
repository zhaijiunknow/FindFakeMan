using System;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Core.Runtime.Managers
{
    /// <summary>
    /// 场景流转。**所有**切场景都应该走这里（ButtonAction / MainMenuController / VN 的 EndAction.LoadScene）。
    ///
    /// 转场：默认用 <see cref="ScreenTransition"/> 的终端风格过场 ——
    /// 出场是「CRT 关机」（上下黑边合拢 + 中缝亮线 + 收屏白点），入场是「CRT 开机」（亮线向上下展开）。
    /// 目标场景自己也带淡入时（序幕的 BlackOverlay），它会在 ScreenTransition.IsBlack 为真时自动跳过自己的淡入，
    /// 所以不会出现"展开完又渐入一次黑"的两层转场。
    /// </summary>
    public sealed class SceneFlowManager : ManagerBehaviour
    {
        [Header("转场")]
        [Tooltip("切场景时是否走终端风格过场（CRT 关机 → 加载 → CRT 开机）。关掉就是硬切。")]
        [SerializeField] private bool useTransition = true;
        [Tooltip("出场（CRT 关机）时长，秒，不受 timeScale 影响。")]
        [SerializeField] private float collapseDuration = 0.8f;
        [Tooltip("入场（CRT 开机）时长，秒。目标场景自带黑幕时也可以设 0。")]
        [SerializeField] private float expandDuration = 1f;

        /// <summary>是否正在切换场景（用来防重复触发）。</summary>
        public bool IsLoading { get; private set; }

        public async UniTask LoadSceneAsync(string sceneName)
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
                if (useTransition)
                {
                    await ScreenTransition.CollapseAsync(collapseDuration);
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
                    if (useTransition)
                    {
                        // 不管成功失败都把黑幕收回去（成功时就是新场景的开机过场）。
                        await ScreenTransition.ExpandAsync(expandDuration);
                    }
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }
    }
}
