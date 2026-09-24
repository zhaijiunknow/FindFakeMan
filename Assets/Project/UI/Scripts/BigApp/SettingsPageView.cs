using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 小软件的「设置」页（照参考图 System Setting 做）：
    /// 显示模式（全屏 / 窗口，带选中态）→ `Screen.fullScreen`；
    /// 主音量 / 音乐音量 / 人物音量 → <see cref="AudioManager"/>；
    /// 返回 → 收起小软件窗口。
    ///
    /// 和别的页面一样**自建版式**（`SettingsPageView` 挂在页面容器上，什么都不用连 ✓），
    /// 因为参考图里的控件在预制体里没有现成的 ✗ —— 但配色和圆角按钮照旧跟项目一致 ✓。
    /// </summary>
    public sealed class SettingsPageView : MonoBehaviour
    {
        [Header("配色（和参考图一致）")]
        [SerializeField] private Color plateColor = new Color(0.20f, 0.24f, 0.33f, 1f);
        [SerializeField] private Color buttonColor = new Color(0.62f, 0.72f, 0.96f, 1f);
        [SerializeField] private Color buttonSelectedColor = new Color(0.45f, 0.58f, 0.92f, 1f);
        [SerializeField] private Color barTrackColor = new Color(1f, 1f, 1f, 0.92f);
        [SerializeField] private Color barFillColor = new Color(0.45f, 0.58f, 0.92f, 1f);

        [Header("行为")]
        [SerializeField] private bool buildAtRuntime = true;

        private TextMeshProUGUI titleText;
        private Button fullScreenButton;
        private Button windowedButton;
        private VolumeBar masterBar;
        private VolumeBar musicBar;
        private VolumeBar voiceBar;

        private void Awake()
        {
            if (!buildAtRuntime)
            {
                return;
            }

            var font = ResolveFont();

            // 标题（参考图左上角那个 System Setting）
            titleText = Label(transform, "Title", "System Setting", font, 0.05f, 0.60f, 0.87f, 0.96f, 30,
                new Color(0.88f, 0.92f, 1f), TextAlignmentOptions.Left);

            // 显示模式
            Label(transform, "DisplayLabel", "显示模式", font, 0.10f, 0.32f, 0.70f, 0.79f, 26,
                new Color(0.86f, 0.90f, 0.98f), TextAlignmentOptions.Left);
            fullScreenButton = Choice(transform, "Choice_FullScreen", "全屏", font, 0.34f, 0.50f, 0.69f, 0.80f);
            windowedButton = Choice(transform, "Choice_Windowed", "窗口", font, 0.52f, 0.68f, 0.69f, 0.80f);
            fullScreenButton.onClick.AddListener(() => SetFullScreen(true));
            windowedButton.onClick.AddListener(() => SetFullScreen(false));

            // 三条音量
            masterBar = VolumeRow(font, "主音量", 0.56f, 0.65f);
            musicBar = VolumeRow(font, "音乐音量", 0.43f, 0.52f);
            voiceBar = VolumeRow(font, "人物音量", 0.30f, 0.39f);

            // 返回
            var back = Choice(transform, "Choice_Back", "返回", font, 0.36f, 0.64f, 0.06f, 0.17f);
            back.onClick.AddListener(CloseWindow);

            RefreshDisplayMode();
            PullAudioVolumes();
        }

        // ---------- 行为 ----------

        private void SetFullScreen(bool full)
        {
            Screen.fullScreen = full;
            RefreshDisplayMode();
            Debug.Log($"[Settings] 显示模式 → {(full ? "全屏" : "窗口")}");
        }

        private void RefreshDisplayMode()
        {
            var full = Screen.fullScreen;
            SetButtonSelected(fullScreenButton, full);
            SetButtonSelected(windowedButton, !full);
        }

        private void PullAudioVolumes()
        {
            if (!Services.TryGet<AudioManager>(out var audio))
            {
                return;
            }

            masterBar?.Configure(masterBar.GetComponent<RectTransform>(), masterBar.transform.Find("Fill")?.GetComponent<Image>(),
                audio.MasterVolume, false);
            musicBar?.Configure(musicBar.GetComponent<RectTransform>(), musicBar.transform.Find("Fill")?.GetComponent<Image>(),
                audio.MusicVolume, false);
            voiceBar?.Configure(voiceBar.GetComponent<RectTransform>(), voiceBar.transform.Find("Fill")?.GetComponent<Image>(),
                audio.VoiceVolume, false);

            masterBar!.ValueChanged += v => audio.SetMasterVolume(v);
            musicBar!.ValueChanged += v => audio.SetMusicVolume(v);
            voiceBar!.ValueChanged += v => audio.SetVoiceVolume(v);
        }

        private void CloseWindow()
        {
            // 小软件的 UIWindowManager 就在根上：按类型名找它（不引 namespace ✗），用 SendMessage 调 Collapse（不猜类型 ✗）。
            var root = transform.root;
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == "UIWindowManager")
                {
                    behaviour.SendMessage("Collapse", SendMessageOptions.DontRequireReceiver);
                    Debug.Log("[Settings] 返回 → 收起小软件窗口。");
                    return;
                }
            }

            Debug.LogWarning("[Settings] 没找到 UIWindowManager，返回按钮收不起窗口。");
        }

        // ---------- 搭控件 ----------

        private VolumeBar VolumeRow(TMP_FontAsset font, string label, float yMin, float yMax)
        {
            Label(transform, $"{label}Label", label, font, 0.10f, 0.32f, yMin, yMax, 26,
                new Color(0.86f, 0.90f, 0.98f), TextAlignmentOptions.Left);

            // 轨道（吃点击）+ 填充
            var trackGo = new GameObject($"{label}Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VolumeBar));
            trackGo.transform.SetParent(transform, false);
            var trackRect = (RectTransform)trackGo.transform;
            trackRect.anchorMin = new Vector2(0.34f, yMin + 0.01f);
            trackRect.anchorMax = new Vector2(0.86f, yMax - 0.02f);
            trackRect.offsetMin = Vector2.zero;
            trackRect.offsetMax = Vector2.zero;

            var trackImage = trackGo.GetComponent<Image>();
            trackImage.color = barTrackColor;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(trackRect, false);
            var fillImage = fillGo.GetComponent<Image>();
            fillImage.color = barFillColor;
            fillImage.raycastTarget = false;

            var bar = trackGo.GetComponent<VolumeBar>();
            bar.Configure(trackRect, fillImage, 0.6f);
            return bar;
        }

        private Button Choice(Transform parent, string name, string label, TMP_FontAsset font,
            float xMin, float xMax, float yMin, float yMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = buttonColor;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var text = Label(rect, "Label", label, font, 0f, 1f, 0f, 1f, 26, Color.white, TextAlignmentOptions.Center);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static void SetButtonSelected(Button button, bool selected)
        {
            if (button == null || button.targetGraphic == null)
            {
                return;
            }

            var image = button.targetGraphic as Image;
            if (image == null)
            {
                return;
            }

            var color = image.color;
            image.color = new Color(selected ? 0.45f : 0.62f, selected ? 0.58f : 0.72f, 0.92f, selected ? 1f : 0.75f);
        }

        private static TextMeshProUGUI Label(Transform parent, string name, string content, TMP_FontAsset font,
            float xMin, float xMax, float yMin, float yMax, float fontSize, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }

            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            return text;
        }

        private TMP_FontAsset ResolveFont()
        {
            var canvas = GetComponentInParent<Canvas>(true);
            var host = canvas != null ? canvas.transform : transform.root;
            foreach (var text in host.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text != null && text.font != null)
                {
                    return text.font;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }
    }
}
