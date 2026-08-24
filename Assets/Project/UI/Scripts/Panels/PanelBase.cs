using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Project.Core.Runtime.Framework;
using UnityEngine;

namespace Project.UI.Panels
{
    /// <summary>
    /// 抽象面板基类：封装面板生命周期（开/关/被压栈/被恢复）、数据/结果、开合动画分派、Esc 默认行为。
    ///
    /// 开合动画分派：
    /// - 有 UIWindowManager（Window != null）→ 用 Window.Expand()/Collapse()（窗口型动画），等待其 animationDuration；
    /// - 否则用 CanvasGroup.DOFade 淡入淡出，等待 fadeDuration。
    ///
    /// 生命周期由 PanelManager 驱动（OpenPanelAsync/ClosePanelAsync），实例默认开时实例化、关时销毁（destroyOnClose）。
    /// </summary>
    public abstract class PanelBase : MonoBehaviour
    {
        [SerializeField] private string panelId;
        [SerializeField] private PanelKind kind = PanelKind.Page;
        [SerializeField] private bool allowMultiple;          // 同 id 是否允许多实例
        [SerializeField] private bool destroyOnClose = true;  // false = 关闭回池（预留）
        [SerializeField] private float fadeDuration = 0.25f;  // 非窗口面板淡入淡出
        [SerializeField] private UIWindowManager windowOverride; // 为空自动 GetComponent

        private UniTaskCompletionSource<object> closeTcs;

        public string PanelId => panelId;
        public PanelKind Kind => kind;
        public bool AllowMultiple => allowMultiple;
        public bool DestroyOnClose => destroyOnClose;

        /// <summary>由 PanelManager 实例化时用注册表的 id/kind 覆盖（保证与注册一致）。</summary>
        public void Bind(string id, PanelKind panelKind)
        {
            panelId = id;
            kind = panelKind;
        }
        public bool IsOpen { get; private set; }
        public object Data { get; private set; }
        public object Result { get; private set; }
        public CanvasGroup CanvasGroup { get; private set; }
        public UIWindowManager Window { get; private set; }

        /// <summary>被压栈（下方有新面板盖上来）。</summary>
        protected virtual UniTask OnPushedAsync(PanelBase above, CancellationToken ct) => UniTask.CompletedTask;
        /// <summary>弹栈恢复（上方面板关闭后回到栈顶）。</summary>
        protected virtual UniTask OnPoppedAsync(PanelBase above, CancellationToken ct) => UniTask.CompletedTask;
        /// <summary>打开时进入。data 为打开参数。</summary>
        protected abstract UniTask OnOpenAsync(object data, CancellationToken ct);
        /// <summary>关闭时进入。result 为关闭结果。</summary>
        protected abstract UniTask OnCloseAsync(object result, CancellationToken ct);

        /// <summary>
        /// 面板打开。已打开且非多实例时仅更新 Data（回顶，不重建）。
        /// </summary>
        public async UniTask OpenAsync(object data, CancellationToken ct)
        {
            Log($"OpenAsync(id={panelId}, alreadyOpen={IsOpen})");
            if (IsOpen && !allowMultiple)
            {
                Data = data;
                return;
            }

            Data = data;
            EnsureRuntimeComponents();

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            IsOpen = true;
            await AnimateOpenAsync(ct);
            await OnOpenAsync(data, ct);
        }

        /// <summary>
        /// 面板关闭。返回关闭结果。关闭后的隐藏/销毁由 PanelManager 依据 destroyOnClose 处理。
        /// </summary>
        public async UniTask<object> CloseAsync(object result, CancellationToken ct)
        {
            Log($"CloseAsync(id={panelId}, open={IsOpen})");
            if (!IsOpen)
            {
                return null;
            }

            Result = result;
            await AnimateCloseAsync(ct);
            await OnCloseAsync(result, ct);
            IsOpen = false;
            closeTcs?.TrySetResult(result);
            return result;
        }

        /// <summary>等待本面板被关闭。用 UniTaskCompletionSource 拿到关闭结果。</summary>
        public UniTask<object> WaitForCloseAsync()
        {
            closeTcs = new UniTaskCompletionSource<object>();
            return closeTcs.Task;
        }

        /// <summary>设置结果并关闭自己（由 PanelManager 关栈）。</summary>
        public void SetResultAndClose(object result = null)
        {
            Result = result;
            if (Services.TryGet<PanelManager>(out var manager))
            {
                manager.ClosePanelAsync(this, result).Forget();
            }
            else
            {
                Log("SetResultAndClose: 无 PanelManager，无法关闭");
            }
        }

        /// <summary>Esc 默认行为：Overlay 关闭自己；Page 由子类覆写。</summary>
        public virtual void OnBackPressed()
        {
            if (Kind == PanelKind.Overlay && Services.TryGet<PanelManager>(out var manager))
            {
                manager.CloseTopAsync().Forget();
            }
            else
            {
                Log("OnBackPressed: Page 需子类覆写处理 Esc");
            }
        }

        protected void EnsureRuntimeComponents()
        {
            CanvasGroup = GetComponent<CanvasGroup>();
            Window = windowOverride != null ? windowOverride : GetComponent<UIWindowManager>();
        }

        private async UniTask AnimateOpenAsync(CancellationToken ct)
        {
            if (Window != null)
            {
                DOTween.Kill(gameObject);
                Window.Expand();
                await WaitSeconds(Window.animationDuration, ct);
            }
            else
            {
                if (CanvasGroup == null)
                {
                    CanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }

                CanvasGroup.alpha = 0f;
                DOTween.Kill(CanvasGroup);
                CanvasGroup.DOFade(1f, fadeDuration);
                await WaitSeconds(fadeDuration, ct);
            }
        }

        private async UniTask AnimateCloseAsync(CancellationToken ct)
        {
            if (Window != null)
            {
                DOTween.Kill(gameObject);
                Window.Collapse();
                await WaitSeconds(Window.animationDuration, ct);
            }
            else
            {
                if (CanvasGroup == null)
                {
                    CanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }

                DOTween.Kill(CanvasGroup);
                CanvasGroup.DOFade(0f, fadeDuration);
                await WaitSeconds(fadeDuration, ct);
            }
        }

        private static async UniTask WaitSeconds(float seconds, CancellationToken ct)
        {
            if (seconds <= 0f)
            {
                await UniTask.Yield(ct);
            }
            else
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: ct);
            }
        }

        private void OnDestroy()
        {
            DOTween.Kill(CanvasGroup);
            DOTween.Kill(gameObject);
            closeTcs?.TrySetResult(Result);
        }

        private static void Log(string message)
        {
            Debug.Log($"[Panel:{message}]");
        }
    }
}
