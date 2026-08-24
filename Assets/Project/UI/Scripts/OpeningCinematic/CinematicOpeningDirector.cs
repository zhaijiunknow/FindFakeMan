using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using Project.UI.Panels;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.UI.OpeningCinematic
{
    /// <summary>
    /// 开场动画导演（原型）：
    /// 电影相机从「房间全景」推镜到「电脑屏幕」，到位后把菜单画布从
    /// 「渲染到显示器 RenderTexture」交接为「主相机全屏可交互 UI」。
    ///
    /// 核心思路：
    /// 1. 菜单只有一份实例，位于 Screen Space - Camera 画布，由 uiCaptureCamera 渲进 RenderTexture；
    /// 2. 显示器 quad 贴这张 RT，电影相机推镜接近时能看到桌面在显示器上；
    /// 3. 相机到位（显示器充满画面）瞬间，把画布 worldCamera 切到主相机并关闭显示器/背景/捕获相机——
    ///    因为交接的是同一画布同一布局，画面连续，输入随即解锁。
    ///
    /// 相机全程在显示器前方朝 +Z（LookAt 目标也在前方），永不穿越 → 不会 180° 翻转。
    ///
    /// 推镜用 Director 自己的 UniTask 循环按帧驱动（Time.deltaTime 累计），不依赖 DOTween 的全局更新——
    /// 这样即使场景里没有 DOTweenComponent 也能稳定推进，且「任意键跳过」更直接。
    /// </summary>
    public sealed class CinematicOpeningDirector : MonoBehaviour
    {
        [Header("相机")]
        [SerializeField] private Camera cinematicCamera;   // 做推镜的电影相机（通常 = 主相机）
        [SerializeField] private Camera uiCaptureCamera;   // 把菜单画布渲染进 RenderTexture 的捕获相机

        [Header("景")]
        [SerializeField] private Transform startAnchor;    // 推镜起点（房间全景充满画面）
        [SerializeField] private Transform endAnchor;      // 推镜终点（显示器充满画面）
        [SerializeField] private Transform lookTarget;     // 推镜全程注视的点（显示器中心）
        [SerializeField] private MeshRenderer monitorRenderer; // 显示器 quad，运行时替换成 RT 材质
        [SerializeField] private MeshRenderer backgroundRenderer; // 房间背景 quad，交接后收场

        [Header("UI")]
        [SerializeField] private Canvas panelCanvas;       // 面板栈画布（Screen Space - Camera，渲进 RT 显示在显示器）
        [SerializeField] private EventSystem eventSystem;  // 开场期间禁用，交接后启用

        [Header("黑边 (Letterbox)")]
        [SerializeField] private RectTransform letterboxTopBar;    // 顶部黑边（屏幕外上方，推镜时下移）
        [SerializeField] private RectTransform letterboxBottomBar; // 底部黑边（屏幕外下方，推镜时上移）
        [SerializeField] private bool letterboxEnabled = true;
        [Tooltip("每条黑边的高度（朝画面中心移入的像素）。黑边放屏幕外，动画向中心移入这个距离")] [SerializeField] private float letterboxOffset = 150f;
        [SerializeField] private float letterboxAppearDuration = 0.4f;
        [SerializeField] private float letterboxLeaveDuration = 0.5f;

        [Header("参数")]
        [SerializeField] private int captureWidth = 1920;
        [SerializeField] private int captureHeight = 1080;
        [SerializeField] private float dollyDuration = 3f;
        [SerializeField] private bool skipOnAnyKey = true;
        [SerializeField] private bool logEvents = true;
        [SerializeField] private bool captureDebugFrames = false; // 调试用：关键帧存 PNG

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        private RenderTexture renderTexture;
        private Material monitorMaterial;
        private CancellationTokenSource cts;
        private bool completed;
        private bool awaitingStart;   // 等待玩家点击触发推镜（未点击则停在 Room）
        private bool capturedRoom;
        private bool capturedMid;

        private void Awake()
        {
            cts = new CancellationTokenSource();
            CreateRenderTarget();
        }

        private void Start()
        {
            // 停在 Room Background：设好画布与锁定输入，等玩家任意点击才开始推镜。
            PrepareIntro();
            awaitingStart = true;
        }

        private void Update()
        {
            if (awaitingStart && AnyInputPressed)
            {
                awaitingStart = false;
                RunOpeningAsync().Forget();
            }
            // 播放中（awaitingStart=false 且未完成）忽略任何输入，保证完整播放。
        }

        private static bool AnyInputPressed => Input.anyKeyDown || Input.GetMouseButtonDown(0);

        /// <summary>开场准备：锁定输入、把面板画布设为 ScreenSpace-Camera（渲进 RT → 显示器），停在 Room 等输入。</summary>
        private void PrepareIntro()
        {
            SetInputLocked(true);
            if (panelCanvas != null)
            {
                panelCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                panelCanvas.worldCamera = uiCaptureCamera;
            }

            Log("[Cinematic] 等待任意点击开始推镜");
        }

        private void OnDestroy()
        {
            cts?.Cancel();
            cts?.Dispose();
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
            if (monitorMaterial != null)
            {
                Destroy(monitorMaterial);
            }
        }

        /// <summary>创建渲染目标并贴到显示器。RT / 材质在运行时生成，原型阶段不落盘。</summary>
        private void CreateRenderTarget()
        {
            if (uiCaptureCamera == null)
            {
                Debug.LogError("[CinematicOpening] uiCaptureCamera 未指定。");
                return;
            }

            if (monitorRenderer == null)
            {
                Debug.LogError("[CinematicOpening] monitorRenderer 未指定。");
                return;
            }

            renderTexture = new RenderTexture(captureWidth, captureHeight, 24)
            {
                name = "OpeningMenuRT",
            };
            renderTexture.Create();

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Texture");
            }

            monitorMaterial = new Material(shader)
            {
                name = "OpeningMenuMat",
            };
            monitorMaterial.SetTexture(BaseMap, renderTexture);
            // 2.5D 广告牌双面可见：关掉背面剔除，避免朝向判断出错时整块消失。
            monitorMaterial.SetFloat("_Cull", 0f);
            // 与背景同 z=40：用 ZTest Always + 透明队列，让屏幕稳定盖在背景上（避免 z-fighting 闪烁）。
            monitorMaterial.SetFloat("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            monitorMaterial.renderQueue = 3000;

            monitorRenderer.sharedMaterial = monitorMaterial;
            uiCaptureCamera.targetTexture = renderTexture;
        }

        private async UniTaskVoid RunOpeningAsync()
        {
            try
            {
                SetInputLocked(true);
                Log("[Cinematic] 开场开始");

                if (captureDebugFrames)
                {
                    CaptureDebugFrame("frame_00_initial.png");
                }

                // 推镜与黑边并行：黑边收尾在推镜结束同时完成（推镜完黑边已退完）。
                var dollyTask = DollyAsync(cts.Token);
                var letterboxTask = letterboxEnabled
                    ? PlayLetterboxAsync(dollyDuration, cts.Token)
                    : UniTask.CompletedTask;
                await UniTask.WhenAll(dollyTask, letterboxTask);
                if (cts.IsCancellationRequested)
                {
                    return;
                }

                Handoff();
                await ExpandSmallAppAsync(cts.Token); // 推镜结束弹 SmallApp（默认关闭 → 展开）
                Log("[Cinematic] 开场结束，已交接并弹出 SmallApp");
            }
            catch (OperationCanceledException)
            {
                // 场景销毁导致的取消，静默返回。
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CinematicOpening] {ex}");
            }
            finally
            {
                completed = true;
                SetInputLocked(false);
            }
        }

        /// <summary>相机从 startAnchor 推镜到 endAnchor，全程注视 lookTarget。按帧完整播放，不跳过、不可中断。</summary>
        private async UniTask DollyAsync(CancellationToken ct)
        {
            if (cinematicCamera == null || startAnchor == null || endAnchor == null)
            {
                Debug.LogError("[CinematicOpening] 相机或锚点未指定，跳过推镜直接交接。");
                Handoff();
                return;
            }

            var startPos = startAnchor.position;
            var endPos = endAnchor.position;
            // 注视点：起点看向背景中心（背景填满全屏），终点看向屏幕中心，按进度插值 → 平滑转镜。
            var backgroundCenter = backgroundRenderer != null ? backgroundRenderer.transform.position : startPos + Vector3.forward * 10f;
            var startLook = backgroundCenter;
            var endLook = lookTarget != null ? lookTarget.position : endPos + cinematicCamera.transform.forward;

            var elapsed = 0f;
            Log($"[Cinematic] Dolly start dur={dollyDuration}");

            while (elapsed < dollyDuration)
            {
                ct.ThrowIfCancellationRequested();

                float t = Mathf.Clamp01(elapsed / dollyDuration);
                float e = InOutCubic(t);
                cinematicCamera.transform.position = Vector3.Lerp(startPos, endPos, e);
                cinematicCamera.transform.LookAt(Vector3.Lerp(startLook, endLook, e));

                if (captureDebugFrames)
                {
                    if (!capturedRoom && t > 0.05f)
                    {
                        capturedRoom = true;
                        CaptureDebugFrame("frame_01_room.png");
                    }
                    else if (!capturedMid && t > 0.55f)
                    {
                        capturedMid = true;
                        CaptureDebugFrame("frame_02_mid.png");
                    }
                }

                await UniTask.Yield(ct);
                elapsed += Time.deltaTime;
            }

            // 自然完成、跳过或取消，都先把相机定到终点。
            cinematicCamera.transform.position = endPos;
            cinematicCamera.transform.LookAt(endLook);
        }

        /// <summary>缓动：InOutCubic。</summary>
        private static float InOutCubic(float t)
        {
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        /// <summary>
        /// 黑边完整时序，与推镜同长（total）：先移入、停留、最后在推镜结束的同时移出完毕。
        /// 这样「推镜结束黑边就已退出完毕」。
        /// </summary>
        private async UniTask PlayLetterboxAsync(float total, CancellationToken ct)
        {
            if (letterboxTopBar == null || letterboxBottomBar == null || total <= 0f)
            {
                return;
            }

            var half = 540f + letterboxOffset * 0.5f;
            var topRest = new Vector2(0f, half);
            var botRest = new Vector2(0f, -half);
            var topShown = new Vector2(0f, half - letterboxOffset);
            var botShown = new Vector2(0f, -(half - letterboxOffset));

            // 移入。
            await LerpLetterboxAsync(letterboxTopBar, topRest, topShown,
                letterboxBottomBar, botRest, botShown, letterboxAppearDuration, ct);

            // 停留（直到推镜结束前的 leave 时长）。
            var remain = total - letterboxAppearDuration - letterboxLeaveDuration;
            if (remain > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(remain), cancellationToken: ct);
            }

            // 移出，结束时刻 = 推镜结束。
            await LerpLetterboxAsync(letterboxTopBar, topShown, topRest,
                letterboxBottomBar, botShown, botRest, letterboxLeaveDuration, ct);
        }

        private async UniTask LerpLetterboxAsync(
            RectTransform top, Vector2 topFrom, Vector2 topTo,
            RectTransform bottom, Vector2 bottomFrom, Vector2 bottomTo,
            float duration, CancellationToken ct)
        {
            if (top == null || bottom == null || duration <= 0f)
            {
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                float t = Mathf.Clamp01(elapsed / duration);
                top.anchoredPosition = Vector2.Lerp(topFrom, topTo, t);
                bottom.anchoredPosition = Vector2.Lerp(bottomFrom, bottomTo, t);
                await UniTask.Yield(ct);
                elapsed += Time.deltaTime;
            }

            top.anchoredPosition = topTo;
            bottom.anchoredPosition = bottomTo;
        }

        /// <summary>推镜结束弹出 SmallApp 窗口（SmallApp 默认关闭，此处展开）。</summary>
        private async UniTask ExpandSmallAppAsync(CancellationToken ct)
        {
            var window = FindObjectOfType<UIWindowManager>();
            if (window == null)
            {
                Log("[Cinematic] 未找到 UIWindowManager，跳过弹窗");
                return;
            }

            window.Expand();
            await UniTask.Delay(TimeSpan.FromSeconds(window.animationDuration), cancellationToken: ct);
        }

        /// <summary>交接：面板画布切到主相机全屏，收掉电影场景。</summary>
        private void Handoff()
        {
            if (panelCanvas != null && cinematicCamera != null)
            {
                // 主相机在推镜阶段排除了 UI 层（避免 UI 直接上屏），交接时加回来。
                cinematicCamera.cullingMask |= 1 << 5;
                panelCanvas.worldCamera = cinematicCamera;
            }

            if (monitorRenderer != null)
            {
                monitorRenderer.gameObject.SetActive(false);
            }

            if (backgroundRenderer != null)
            {
                backgroundRenderer.gameObject.SetActive(false);
            }

            if (uiCaptureCamera != null)
            {
                uiCaptureCamera.gameObject.SetActive(false);
            }

            if (captureDebugFrames)
            {
                CaptureDebugFrame("frame_03_handoff.png");
            }
        }

        private void SetInputLocked(bool locked)
        {
            if (eventSystem != null)
            {
                eventSystem.enabled = !locked;
            }
        }

        /// <summary>原型调试：直接把主相机渲染到 RenderTexture 存成 PNG（绕开 Game view）。</summary>
        private void CaptureDebugFrame(string fileName)
        {
            if (cinematicCamera == null)
            {
                return;
            }

            var cam = cinematicCamera;
            var rt = RenderTexture.GetTemporary(960, 540, 24);
            var oldTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = oldTarget;

            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            var oldActive = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = oldActive;

            RenderTexture.ReleaseTemporary(rt);

            var dir = System.IO.Path.Combine(Application.dataPath, "../.unity/capture/debug");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, fileName);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);

            Log($"[Cinematic] 已截帧：{fileName}");
        }

        private void Log(string message)
        {
            if (logEvents)
            {
                Debug.Log(message);
            }
        }
    }
}
