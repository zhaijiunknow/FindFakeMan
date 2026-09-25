using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 小软件「设置」页（照参考图 System Setting ✓，也是其它子页的视觉基准 ✓）：
    /// 显示模式（全屏 / 窗口 ✓）→ `Screen.fullScreen`；三条音量 → <see cref="AudioManager"/>；
    /// 返回 → 回分页菜单 ✓（关窗口交给右上红点 ✓）。
    ///
    /// 两段式（为了控件能烘进场景 ✓）：<see cref="Build"/> 建控件、<see cref="WireEvents"/> 接点击（Awake 统一做 ✓）、
    /// <see cref="HookAudio"/> 把音量条接到 AudioManager（也放在 Awake ✓，两条路都要 ✓）。
    /// </summary>
    public sealed class SettingsPageView : MonoBehaviour
    {
        [Header("行为")]
        [Tooltip("自己搭版式。建造工具烘场景时会设成 false ✓。")]
        [SerializeField] private bool buildAtRuntime = true;

        [Header("控件引用（建造工具烘场景时写入 ✓）")]
        [SerializeField] private Button fullScreenButton;

        [Tooltip("滑块圆点用的图 ✓（美术给的 slide.png ✓）。由建造工具填 ✓ —— 运行时脚本不能用 AssetDatabase ✗。")]
        [SerializeField] private Sprite sliderHandleSprite;
        [SerializeField] private Button windowedButton;
        [SerializeField] private VolumeBar masterBar;
        [SerializeField] private VolumeBar musicBar;
        [SerializeField] private VolumeBar voiceBar;
        [SerializeField] private Button backButton;

        private bool wired;
        private bool audioHooked;

        private void Awake()
        {
            if (buildAtRuntime)
            {
                Build();
            }

            WireEvents();
            HookAudio();
        }

        /// <summary>建控件（不接点击 ✗）。</summary>
        public void Build()
        {
            var font = SmallAppPageStyle.ResolveFont(this);

            SmallAppPageStyle.Title(transform, font, "System Setting");

            SmallAppPageStyle.RowLabel(transform, "DisplayLabel", "显示模式", font, 0.70f, 0.79f);
            fullScreenButton = SmallAppPageStyle.Choice(transform, "Choice_FullScreen", "全屏", font, 0.34f, 0.50f, 0.69f, 0.80f);
            windowedButton = SmallAppPageStyle.Choice(transform, "Choice_Windowed", "窗口", font, 0.52f, 0.68f, 0.69f, 0.80f);

            masterBar = VolumeRow(font, "主音量", 0.56f, 0.65f);
            musicBar = VolumeRow(font, "音乐音量", 0.43f, 0.52f);
            voiceBar = VolumeRow(font, "人物音量", 0.30f, 0.39f);

            backButton = SmallAppPageStyle.BackButton(transform, font, null);

            RefreshDisplayMode();
        }

        /// <summary>接点击（幂等 ✓）。</summary>
        public void WireEvents()
        {
            if (wired)
            {
                return;
            }

            wired = true;

            if (fullScreenButton != null)
            {
                fullScreenButton.onClick.RemoveListener(OnClickFullScreen);
                fullScreenButton.onClick.AddListener(OnClickFullScreen);
            }

            if (windowedButton != null)
            {
                windowedButton.onClick.RemoveListener(OnClickWindowed);
                windowedButton.onClick.AddListener(OnClickWindowed);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(BackToMenu);
                backButton.onClick.AddListener(BackToMenu);
            }
        }

        /// <summary>把音量条接到 AudioManager（幂等 ✓）。两条路（运行时建 / 烘进场景）都要跑 ✓。</summary>
        public void HookAudio()
        {
            if (audioHooked || !Services.TryGet<AudioManager>(out var audio))
            {
                return;
            }

            audioHooked = true;

            // 先把 manager 的当前值灌进三条（UI 反映真实状态 ✓），再订阅拖动。
            masterBar?.SetValue(audio.MasterVolume, false);
            musicBar?.SetValue(audio.MusicVolume, false);
            voiceBar?.SetValue(audio.VoiceVolume, false);

            if (masterBar != null)
            {
                masterBar.ValueChanged -= audio.SetMasterVolume;
                masterBar.ValueChanged += audio.SetMasterVolume;
            }

            if (musicBar != null)
            {
                musicBar.ValueChanged -= audio.SetMusicVolume;
                musicBar.ValueChanged += audio.SetMusicVolume;
            }

            if (voiceBar != null)
            {
                voiceBar.ValueChanged -= audio.SetVoiceVolume;
                voiceBar.ValueChanged += audio.SetVoiceVolume;
            }
        }

        private void OnClickFullScreen() => SetFullScreen(true);
        private void OnClickWindowed() => SetFullScreen(false);

        private void BackToMenu()
        {
            var host = GetComponentInParent<SmallAppPageHost>(true);
            if (host != null)
            {
                host.BackToMenu();
            }
        }

        private void SetFullScreen(bool full)
        {
            Screen.fullScreen = full;
            RefreshDisplayMode();
            Debug.Log($"[Settings] 显示模式 → {(full ? "全屏" : "窗口")}");
        }

        private void RefreshDisplayMode()
        {
            var full = Screen.fullScreen;
            SmallAppPageStyle.SetSelected(fullScreenButton, full);
            SmallAppPageStyle.SetSelected(windowedButton, !full);
        }

        /// <summary>
        /// **只重建三行音量** ✓（给编辑器菜单用 ✓）。
        ///
        /// 为什么需要它 ✗→✓：那三行控件是**烘进场景**的 ✓ —— 场景里存的还是"没有圆点的横条"旧结构 ✗，
        /// 而"重建整个场景"会把你手调过的一切冲掉 ✗。这个方法只删掉旧的三行、按新结构重搭 ✓，
        /// 其余对象一个都不碰 ✓（菜单 `Tools/Project/UI/Rebuild Volume Rows (current scene)` ✓）。
        /// </summary>
        public void RebuildVolumeRows()
        {
            var font = SmallAppPageStyle.ResolveFont(this);

            // 先删掉旧的三行 ✓：Track 会连它下面的 Fill 一起走 ✓；Label 是独立对象 ✓ 也要删 ✓，
            // 不然下面重搭时会多出第二个同名标签 ✗。
            foreach (var label in new[] { "主音量", "音乐音量", "人物音量" })
            {
                RemoveChild($"{label}Track");
                RemoveChild($"{label}Label");
            }

            masterBar = VolumeRow(font, "主音量", 0.56f, 0.65f);
            musicBar = VolumeRow(font, "音乐音量", 0.43f, 0.52f);
            voiceBar = VolumeRow(font, "人物音量", 0.30f, 0.39f);

            // 重新订阅 ✓：`HookAudio` 有 `audioHooked` 闸 ✓，不先放开的话它会把新三条直接跳过 ✗。
            // （编辑期调用时它反正会因为拿不到 AudioManager 而早退 ✓，运行时的 Awake 会再正常接一次 ✓。）
            audioHooked = false;
            HookAudio();
        }

        private void RemoveChild(string childName)
        {
            var child = transform.Find(childName);
            if (child == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        /// <summary>
        /// 一行音量：左边标签、右边一条**标准 uGUI `Slider`** ✓（轨道 + 填充 + `slide.png` 圆点 ✓）。
        ///
        /// 层级按 Unity 的约定搭 ✓：`Track`（Slider 挂这儿 ✓，它负责点/拖 ✓）
        /// ├─ `Fill Area/Fill`（Slider 会按值缩放它的宽度 ✓）
        /// └─ `Handle Slide Area/Handle`（圆点 ✓）
        /// 两层 Area 都左右各留了内缩 ✓ —— 不然圆点会在两端被裁掉一半 ✗。
        /// </summary>
        private VolumeBar VolumeRow(TMP_FontAsset font, string label, float yMin, float yMax)
        {
            SmallAppPageStyle.RowLabel(transform, $"{label}Label", label, font, yMin, yMax);

            var trackGo = new GameObject($"{label}Track",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider), typeof(VolumeBar));
            trackGo.transform.SetParent(transform, false);

            var trackRect = (RectTransform)trackGo.transform;
            trackRect.anchorMin = new Vector2(0.34f, yMin + 0.01f);
            trackRect.anchorMax = new Vector2(0.86f, yMax - 0.02f);
            trackRect.offsetMin = Vector2.zero;
            trackRect.offsetMax = Vector2.zero;

            var trackImage = trackGo.GetComponent<Image>();
            trackImage.color = SmallAppPageStyle.TrackColor;

            // ---- 填充 ----
            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(trackRect, false);
            var fillArea = (RectTransform)fillAreaGo.transform;
            fillArea.anchorMin = new Vector2(0f, 0.34f);
            fillArea.anchorMax = new Vector2(1f, 0.66f);
            fillArea.offsetMin = new Vector2(8f, 0f);
            fillArea.offsetMax = new Vector2(-8f, 0f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(fillArea, false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var fillImage = fillGo.GetComponent<Image>();
            fillImage.color = SmallAppPageStyle.FillColor;
            fillImage.raycastTarget = false; // 填充别抢点击 ✓（要点落在整条轨道上 ✓）

            // ---- 圆点 ----
            var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaGo.transform.SetParent(trackRect, false);
            var handleArea = (RectTransform)handleAreaGo.transform;
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(12f, 0f);
            handleArea.offsetMax = new Vector2(-12f, 0f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handleGo.transform.SetParent(handleArea, false);
            var handleRect = (RectTransform)handleGo.transform;
            handleRect.sizeDelta = new Vector2(24f, 24f);

            var handleImage = handleGo.GetComponent<Image>();
            handleImage.sprite = sliderHandleSprite;
            handleImage.color = Color.white;
            handleImage.preserveAspect = true;
            handleImage.raycastTarget = false; // 圆点不吃射线 ✓：拖动由 Slider 在整条轨道上接 ✓（点哪儿跳哪儿 ✓）

            // ---- Slider 本体 ----
            var slider = trackGo.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = trackImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.transition = Selectable.Transition.None; // 别给轨道染悬停色 ✓（那会跟美术打架 ✗）
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 0.6f;

            var bar = trackGo.GetComponent<VolumeBar>();
            bar.Configure(slider, 0.6f, false);
            return bar;
        }
    }
}
