using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.UI.Panels
{
    /// <summary>
    /// 面板栈管理器。单一 Screen Space Overlay 底座 + 栈（自底向上）。
    ///
    /// 可见性规则（不变量）：只显示「栈中最后一个 Page 及其上方所有面板」，其余 SetActive(false)。
    ///   - [Page A]               → A
    ///   - [Page A, Overlay B]    → A+B（B 的底板盖住 A）
    ///   - [Page A, Page B]       → B（A 被隐藏）
    ///   - [Page A, Overlay B, Page C] → C（弹栈恢复 B、A）
    ///
    /// 串行化：所有 push/pop 用 SemaphoreSlim(1,1) 排他；连点/快速开关由锁 + 非多实例 no-op 兜底。
    /// 继承 ManagerBehaviour：Awake 自动注册进 Services。
    /// </summary>
    public sealed class PanelManager : ManagerBehaviour
    {
        [SerializeField] private RectTransform uiRoot;                // 为空则 EnsureUIRoot() 运行时建
        [SerializeField] private List<PanelEntry> panels = new();
        [SerializeField] private bool handleEscape = true;
        [SerializeField] private bool clearStackOnSceneLoad = true;
        [SerializeField] private bool logStack = true;

        public static PanelManager Instance;

        public event Action<PanelBase> OnPanelOpened;
        public event Action<PanelBase> OnPanelClosed;
        public event Action<IReadOnlyList<PanelBase>> OnStackChanged;

        private readonly List<PanelBase> stack = new();
        private readonly SemaphoreSlim @lock = new(1, 1);
        private CancellationTokenSource disposeCts = new();

        public PanelBase Top => stack.Count > 0 ? stack[^1] : null;
        public int Count => stack.Count;
        public bool IsTransitioning => @lock.CurrentCount == 0;

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
            EnsureUIRoot();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        protected override void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            disposeCts?.Cancel();
            disposeCts?.Dispose();
            @lock.Dispose();
            base.OnDestroy();
        }

        private void Update()
        {
            if (!handleEscape || IsTransitioning)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) && Top != null)
            {
                Top.OnBackPressed();
            }
        }

        // ---------- 注册表 ----------

        private bool TryGetEntry(string panelId, out PanelEntry entry)
        {
            foreach (var e in panels)
            {
                if (e != null && e.PanelId == panelId)
                {
                    entry = e;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        // ---------- 打开 ----------

        public async UniTask<PanelBase> OpenPanelAsync(string panelId, object data = null, CancellationToken ct = default)
        {
            await @lock.WaitAsync(ct);
            try
            {
                if (TryGetEntry(panelId, out var entry) == false || entry.Prefab == null)
                {
                    Debug.LogWarning($"[PanelManager] 未注册的面板：{panelId}");
                    return null;
                }

                // 非多实例：已打开则仅回顶更新数据，不重建。
                if (TryGetOpen(panelId, out var existing) && !existing.AllowMultiple)
                {
                    await existing.OpenAsync(data, ct);
                    BringToTop(existing);
                    LogStack("open-existing");
                    return existing;
                }

                var instance = Instantiate(entry.Prefab, uiRoot);
                Stretch(instance.GetComponent<RectTransform>());

                var panel = instance.GetComponent<PanelBase>();
                if (panel == null)
                {
                    panel = instance.AddComponent<SimplePanel>();
                }

                panel.Bind(panelId, entry.Kind);

                stack.Add(panel);
                await panel.OpenAsync(data, ct);
                ApplyVisibility();
                OnStackChanged?.Invoke(stack);
                OnPanelOpened?.Invoke(panel);
                LogStack("open");
                return panel;
            }
            finally
            {
                @lock.Release();
            }
        }

        // ---------- 关闭 ----------

        public async UniTask<object> ClosePanelAsync(PanelBase panel, object result = null, CancellationToken ct = default)
        {
            if (panel == null)
            {
                return null;
            }

            await @lock.WaitAsync(ct);
            try
            {
                if (!stack.Contains(panel))
                {
                    return null;
                }

                stack.Remove(panel);
                var closeResult = await panel.CloseAsync(result, ct);

                if (panel.DestroyOnClose)
                {
                    Destroy(panel.gameObject);
                }
                else
                {
                    panel.gameObject.SetActive(false);
                }

                ApplyVisibility();
                OnStackChanged?.Invoke(stack);
                OnPanelClosed?.Invoke(panel);
                LogStack("close");
                return closeResult;
            }
            finally
            {
                @lock.Release();
            }
        }

        public async UniTask<object> ClosePanelByIdAsync(string panelId, object result = null, CancellationToken ct = default)
        {
            // 不在外部加锁：ClosePanelAsync 自带锁，避免重入。
            if (TryGetOpen(panelId, out var panel))
            {
                return await ClosePanelAsync(panel, result, ct);
            }

            return null;
        }

        public async UniTask<object> CloseTopAsync(object result = null, CancellationToken ct = default)
        {
            var top = Top;
            if (top == null)
            {
                return null;
            }

            return await ClosePanelAsync(top, result, ct);
        }

        public void CloseAll()
        {
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                var panel = stack[i];
                stack.RemoveAt(i);
                panel.CloseAsync(null, default).Forget();
                if (panel.DestroyOnClose)
                {
                    Destroy(panel.gameObject);
                }
            }

            ApplyVisibility();
            OnStackChanged?.Invoke(stack);
        }

        /// <summary>场景切换：立即销毁全部，不播动画。</summary>
        public void ClearStack()
        {
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                Destroy(stack[i].gameObject);
            }

            stack.Clear();
            ApplyVisibility();
            OnStackChanged?.Invoke(stack);
        }

        public bool IsOpen(string panelId) => TryGetOpen(panelId, out _);

        public void NotifyBackPressed() => Top?.OnBackPressed();

        private void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            if (clearStackOnSceneLoad)
            {
                ClearStack();
            }
        }

        // ---------- 可见性 ----------

        private void ApplyVisibility()
        {
            int lastPage = stack.FindLastIndex(p => p.Kind == PanelKind.Page);
            if (lastPage == -1)
            {
                for (int i = 0; i < stack.Count; i++)
                {
                    stack[i].gameObject.SetActive(true);
                }

                return;
            }

            for (int i = 0; i < stack.Count; i++)
            {
                stack[i].gameObject.SetActive(i >= lastPage);
            }
        }

        private bool TryGetOpen(string panelId, out PanelBase panel)
        {
            foreach (var p in stack)
            {
                if (p != null && p.PanelId == panelId)
                {
                    panel = p;
                    return true;
                }
            }

            panel = null;
            return false;
        }

        private void BringToTop(PanelBase panel)
        {
            var idx = stack.IndexOf(panel);
            if (idx >= 0 && idx < stack.Count - 1)
            {
                stack.RemoveAt(idx);
                stack.Add(panel);
                ApplyVisibility();
                OnStackChanged?.Invoke(stack);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ---------- UI 根 ----------

        /// <summary>面板栈的画布（uiRoot 上的 Canvas），供开屏导演切换渲染相机。</summary>
        public Canvas PanelCanvas => uiRoot != null ? uiRoot.GetComponent<Canvas>() : null;

        /// <summary>设置面板画布的渲染模式与相机（开屏段=ScreenSpaceCamera+捕获相机，交接=主相机）。</summary>
        public void SetPanelRenderMode(RenderMode mode, Camera cam = null)
        {
            var canvas = PanelCanvas;
            if (canvas == null)
            {
                return;
            }

            canvas.renderMode = mode;
            canvas.worldCamera = cam;
        }

        private void EnsureUIRoot()
        {
            if (uiRoot != null)
            {
                return;
            }

            var canvasGo = new GameObject("PanelUIRoot", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(canvasGo.transform, false);
            }

            uiRoot = canvasGo.GetComponent<RectTransform>();
        }

        private void LogStack(string why)
        {
            if (!logStack)
            {
                return;
            }

            var desc = string.Join(" > ", stack.ConvertAll(p => $"{p.PanelId}({p.Kind})"));
            Debug.Log($"[PanelManager:{why}] count={stack.Count} top={(Top != null ? Top.PanelId : "null")}  [{desc}]");
        }
    }
}
