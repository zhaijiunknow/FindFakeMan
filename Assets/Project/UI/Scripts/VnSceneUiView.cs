using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;
using Project.Narrative.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Scripts
{
    /// <summary>
    /// 纯 VN（视觉小说）场景的 UI 视图：实现 ISceneUiView，负责 VN 面板的显示、
    /// 空格/右键推进与选项按钮。游戏性 UI（调查/工具/HUD 等）留空占位。
    /// 文本组件统一使用 TextMeshPro（TextMeshProUGUI）。
    ///
    /// 选项按钮是生成式的：按 SetChoices 传入的选项数量实例化，多余的按钮隐藏并复用。
    /// 配置了 choiceButtonPrefab 就实例化预制体，没配则用代码生成同样式的按钮。
    /// </summary>
    public sealed class VnSceneUiView : MonoBehaviour, ISceneUiView
    {
        [Header("VN 面板")]
        [SerializeField] private GameObject vnPanel;
        [SerializeField] private TextMeshProUGUI vnSpeakerText;
        [SerializeField] private TextMeshProUGUI vnBodyText;

        [Header("VN 控制按钮")]
        [Tooltip("快进按钮：点击切换快进；激活时图标会被染成 toggleActiveColor。")]
        [SerializeField] private Button skipButton;
        [Tooltip("自动播放按钮：点击切换自动播放。")]
        [SerializeField] private Button autoButton;
        [Tooltip("按钮处于激活状态时的着色（图标是白色，会被乘成这个颜色）。")]
        [SerializeField] private Color toggleActiveColor = new Color(0.29f, 0.55f, 1f, 1f);

        [Header("选项按钮（生成式）")]
        [Tooltip("选项按钮的容器。按钮会生成到它下面，容器高度按选项数量自动调整。")]
        [SerializeField] private RectTransform choiceRoot;
        [Tooltip("选项按钮预制体。留空则用代码生成同样式的按钮。")]
        [SerializeField] private Button choiceButtonPrefab;
        [Tooltip("代码兜底按钮的底板图（不配则为纯色块）。")]
        [SerializeField] private Sprite choiceButtonSprite;
        [Tooltip("选项按钮的横向对齐方式。宽度被拉满容器时该项无效。")]
        [SerializeField] private ChoiceAlign choiceAlign = ChoiceAlign.Center;
        [Tooltip("选项整组在容器里的纵向位置。全屏容器 + Center/Middle = 屏幕正中竖排（柚子社那种）。")]
        [SerializeField] private ChoiceVertical choiceVertical = ChoiceVertical.Middle;
        [Tooltip("边距（x=左, y=下, z=右, w=上），画布参考像素。居中时只当作最小留白。")]
        [SerializeField] private Vector4 choiceMargin = new Vector4(120f, 60f, 120f, 60f);
        [Tooltip("按钮高度基准：>0 就用这个高度（覆盖预制体自带高度，场景里可直接调）；0 = 用预制体的高度。" +
                 "文字换行需要更高时仍然会自动长高。注意别低于 ~40，否则九宫格底板的上下圆角会挤在一起。")]
        [SerializeField] private float choiceButtonHeight;
        [Tooltip("按钮宽度模式：Fixed=用预制体/LayoutElement 固定宽；FitText=每颗按自己文字宽；UniformFitText=统一用最长那条的宽度（整齐，推荐）。")]
        [SerializeField] private ChoiceWidthMode choiceWidthMode = ChoiceWidthMode.UniformFitText;
        [Tooltip("FitText/UniformFitText 时在文字宽之外的额外留白（像素）。")]
        [SerializeField] private float choiceTextPadding = 32f;
        [Tooltip("宽度下限：短文字也会至少这么宽（VN 选项一般统一在这个宽度左右）。")]
        [SerializeField] private float choiceMinWidth = 860f;
        [SerializeField] private float choiceMaxWidth = 1200f;
        [Tooltip("打印每次选项布局的实际尺寸，方便核对\"自适应\"有没有生效。")]
        [SerializeField] private bool logChoiceLayout = true;
        [Tooltip("兜底宽度：0 表示横向拉满容器（九宫格底板的推荐用法）。")]
        [SerializeField] private float choiceButtonWidth;
        [SerializeField] private float choiceButtonSpacing = 24f;
        [Tooltip("选项文字大小：>0 直接用它；0 = 按按钮高度 ×0.42 自动（高度也为 0 时则用预制体自己的字号）。")]
        [SerializeField] private float choiceFontSize;

        /// <summary>选项按钮在容器内的对齐方式。</summary>
        public enum ChoiceAlign
        {
            Left,
            Center,
            Right
        }

        /// <summary>选项整组在容器内的纵向位置。</summary>
        public enum ChoiceVertical
        {
            Top,
            Middle,
            Bottom
        }

        /// <summary>选项按钮宽度的决定方式。</summary>
        public enum ChoiceWidthMode
        {
            /// <summary>用预制体 / LayoutElement 上的固定宽度。</summary>
            Fixed,

            /// <summary>每颗按钮按自己的文字宽度。</summary>
            FitText,

            /// <summary>全部按钮统一用最长那条的宽度（整齐居中，推荐）。</summary>
            UniformFitText
        }

        private readonly List<string> currentChoiceIds = new();
        private readonly List<ChoiceButtonEntry> choiceButtonPool = new();
        private int activeChoiceCount;

        /// <summary>一个生成出来的选项按钮及其文本组件。</summary>
        private sealed class ChoiceButtonEntry
        {
            public Button Button;
            public TextMeshProUGUI Label;
        }

        private void Awake()
        {
            Services.Register<ISceneUiView>(this);
            HideChoices();
            SetVnVisible(false);
        }

        private void OnDestroy()
        {
            if (Services.TryGet<VNDirector>(out var vnDirector))
            {
                vnDirector.ModeChanged -= RefreshControlButtons;
            }

            Services.UnregisterInstance(this);
        }

        private void Start()
        {
            // Start 晚于所有 Awake，这时 VNDirector 一定已经注册进 Services。
            if (Services.TryGet<VNDirector>(out var vnDirector))
            {
                vnDirector.ModeChanged += RefreshControlButtons;
            }

            BindControlButton(skipButton, ToggleSkipMode);
            BindControlButton(autoButton, ToggleAutoMode);
            RefreshControlButtons();
        }

        private void Update()
        {
            if (vnPanel == null || !vnPanel.activeSelf || activeChoiceCount > 0)
            {
                return;
            }

            // 空格 / 右键推进（与选项按钮冲突最小）
            if ((Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Space))
                && Services.TryGet<VNDirector>(out var vnDirector))
            {
                vnDirector.Advance().Forget();
            }
        }

        // ---- ISceneUiView: VN ----

        public void SetVnVisible(bool visible)
        {
            if (vnPanel != null)
            {
                vnPanel.SetActive(visible);
            }
        }

        public void SetVnLine(string speakerName, string text)
        {
            SetVnVisible(true);
            if (vnSpeakerText != null)
            {
                vnSpeakerText.text = string.IsNullOrWhiteSpace(speakerName) ? "旁白" : speakerName;
            }

            if (vnBodyText != null)
            {
                vnBodyText.text = string.IsNullOrWhiteSpace(text) ? string.Empty : text;
            }

            // 当前 UI 是瞬显（还没有打字机），所以设置完文本就算这一行显示完了；
            // 以后接打字机时，把这一句挪到打字结束的回调里即可。
            if (Services.TryGet<VNDirector>(out var vnDirector))
            {
                vnDirector.NotifyLineDisplayed();
            }
        }

        public void SetChoices(IReadOnlyList<VNChoiceViewData> choices)
        {
            HideChoices();

            if (choices == null)
            {
                return;
            }

            if (choiceRoot == null)
            {
                Debug.LogWarning("[VN] VnSceneUiView 没有配置 choiceRoot，选项无法显示。", this);
                return;
            }

            var index = 0;
            foreach (var choice in choices)
            {
                var choiceId = choice?.ChoiceId;
                if (choice == null || string.IsNullOrWhiteSpace(choiceId))
                {
                    continue;
                }

                var entry = GetOrCreateChoiceButton(index);
                currentChoiceIds.Add(choiceId);
                if (entry.Label != null)
                {
                    entry.Label.text = choice.Text;
                }

                entry.Button.gameObject.SetActive(true);
                index++;
            }

            activeChoiceCount = index;
            LayoutChoiceButtons(index);
        }

        public void HideChoices()
        {
            currentChoiceIds.Clear();
            activeChoiceCount = 0;

            foreach (var entry in choiceButtonPool)
            {
                if (entry.Button != null)
                {
                    entry.Button.gameObject.SetActive(false);
                }
            }
        }

        // ---- VN 控制按钮（快进 / 自动） ----

        private void ToggleSkipMode()
        {
            if (Services.TryGet<VNDirector>(out var vnDirector))
            {
                vnDirector.ToggleSkipMode();
            }
        }

        private void ToggleAutoMode()
        {
            if (Services.TryGet<VNDirector>(out var vnDirector))
            {
                vnDirector.ToggleAutoMode();
            }
        }

        private static void BindControlButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        /// <summary>按 VNDirector 的状态刷新两个按钮：是否可点 + 激活染色。</summary>
        private void RefreshControlButtons()
        {
            var hasDirector = Services.TryGet<VNDirector>(out var vnDirector);
            var isPlaying = hasDirector && vnDirector.IsPlaying;

            ApplyToggleVisual(skipButton, isPlaying, hasDirector && vnDirector.IsSkipping);
            ApplyToggleVisual(autoButton, isPlaying, hasDirector && vnDirector.IsAutoPlaying);
        }

        private void ApplyToggleVisual(Button button, bool interactable, bool active)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
            if (button.targetGraphic is Image graphic)
            {
                graphic.color = active ? toggleActiveColor : Color.white;
            }
        }

        // ---- ISceneUiView: 游戏性 UI（VN 场景用不到，占位） ----

        public void ShowInspector(Item item, SimpleInteractable interactable) { }
        public void HideInspector() { }
        public void ShowToolDrag(Sprite sprite, Vector2 position) { }
        public void UpdateToolDrag(Vector2 position) { }
        public void HideToolDrag() { }
        public void SetToolDragValidity(bool isValid) { }
        public void SetEvidence(int current, int goal) { }
        public void SetSanity(int current, int max) { }
        public void SetContainment(int current, int max) { }
        public void SetToolbar(IReadOnlyList<ToolItem> tools, int selectedSlot) { }
        public void SetHint(string content, float duration) { }
        public void SetResult(string content, bool highlight) { }

        // ---- 选项按钮生成与布局 ----

        private ChoiceButtonEntry GetOrCreateChoiceButton(int index)
        {
            return index < choiceButtonPool.Count ? choiceButtonPool[index] : CreateChoiceButton();
        }

        private ChoiceButtonEntry CreateChoiceButton()
        {
            var button = choiceButtonPrefab != null
                ? Instantiate(choiceButtonPrefab, choiceRoot, false)
                : CreateRuntimeChoiceButton();

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null)
            {
                Debug.LogWarning($"[VN] 选项按钮 {button.name} 里没有 TextMeshProUGUI，选项文字不会显示。", button);
            }

            // 池下标就是选项下标：SetChoices 按顺序填充，回调里直接用下标取 choiceId。
            var poolIndex = choiceButtonPool.Count;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectChoice(poolIndex));

            var entry = new ChoiceButtonEntry { Button = button, Label = label };
            choiceButtonPool.Add(entry);
            return entry;
        }

        /// <summary>没有配置预制体时，用代码生成一个与原型外观一致的按钮（Image + TMP 文本）。</summary>
        private Button CreateRuntimeChoiceButton()
        {
            var buttonObject = new GameObject("ChoiceButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(choiceRoot, false);

            var image = buttonObject.GetComponent<Image>();
            if (choiceButtonSprite != null)
            {
                // 和预制体同一套规则：没有九宫格边框就按原比例绘制，绝不拉伸变形。
                var hasBorder = choiceButtonSprite.border != Vector4.zero;
                image.sprite = choiceButtonSprite;
                image.type = hasBorder ? Image.Type.Sliced : Image.Type.Simple;
                image.preserveAspect = !hasBorder;
            }
            else
            {
                image.color = new Color(0.22f, 0.24f, 0.32f, 0.95f);
            }

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            // 与预制体保持同一套交互反馈：单张底板图靠 ColorBlock 做悬停/按下。
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1f, 0.97f, 0.9f, 1f),
                pressedColor = new Color(0.82f, 0.84f, 0.9f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 10f);
            labelRect.offsetMax = new Vector2(-10f, -10f);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            if (vnBodyText != null && vnBodyText.font != null)
            {
                // 继承正文的字体资产（中文场景下正文一般是中文字体）。
                label.font = vnBodyText.font;
            }

            var runtimeFontSize = ResolveChoiceFontSize();
            label.fontSize = runtimeFontSize > 0f ? runtimeFontSize : 18f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = Color.white;
            label.raycastTarget = false;

            return button;
        }

        /// <summary>
        /// 把生成出来的按钮竖排。
        /// 容器是「全屏拉伸」时不动它的尺寸，按 choiceAlign × choiceVertical × choiceMargin
        /// 把整组摆到画面中间（柚子社那种居中竖排）；容器是固定尺寸时仍让高度跟随数量。
        /// 尺寸优先取美术信息（LayoutElement → 底板图原始像素），代码里的数值只作兜底。
        /// </summary>
        private void LayoutChoiceButtons(int visibleCount)
        {
            if (choiceRoot == null)
            {
                return;
            }

            // 0) 文字大小随按钮高度走（必须在量文字宽之前应用）
            ApplyChoiceFontSize(visibleCount);

            // 1) 先量尺寸，算出整组高度（居中/贴底都需要先知道总高）
            var sizes = new Vector2[visibleCount];
            var stackHeight = 0f;
            for (var i = 0; i < visibleCount; i++)
            {
                var rect = choiceButtonPool[i].Button.transform as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                sizes[i] = ResolveButtonSize(choiceButtonPool[i].Button, rect);
                stackHeight += sizes[i].y;
            }

            if (visibleCount > 1)
            {
                stackHeight += choiceButtonSpacing * (visibleCount - 1);
            }

            // 1.5) 需要"适应文字"时改写宽度（高度不变，所以不影响上面算好的整组高度）
            ApplyAdaptiveWidths(visibleCount, sizes);

            if (logChoiceLayout)
            {
                LogChoiceLayout(visibleCount, sizes);
            }

            // 2) 整组从容器顶边往下量多少开始摆
            var isStretched = choiceRoot.anchorMin != choiceRoot.anchorMax;
            var startOffset = 0f;
            if (isStretched)
            {
                var containerHeight = choiceRoot.rect.height;
                switch (choiceVertical)
                {
                    case ChoiceVertical.Middle:
                        startOffset = Mathf.Max(choiceMargin.w, (containerHeight - stackHeight) * 0.5f);
                        break;
                    case ChoiceVertical.Bottom:
                        startOffset = Mathf.Max(choiceMargin.w, containerHeight - choiceMargin.y - stackHeight);
                        break;
                    default:
                        startOffset = choiceMargin.w;
                        break;
                }
            }

            // 3) 逐个摆；横向按 choiceAlign，并用 choiceMargin 作最小留白
            var offsetY = startOffset;
            for (var i = 0; i < visibleCount; i++)
            {
                var rect = choiceButtonPool[i].Button.transform as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                var size = sizes[i];
                var insetX = choiceAlign == ChoiceAlign.Left
                    ? choiceMargin.x
                    : choiceAlign == ChoiceAlign.Center
                        ? (choiceMargin.x - choiceMargin.z) * 0.5f
                        : -choiceMargin.z;

                if (size.x <= 0f)
                {
                    // 宽度 0 = 横向拉满容器（左右各留 choiceMargin），此时对齐方式不参与计算。
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.sizeDelta = new Vector2(-(choiceMargin.x + choiceMargin.z), size.y);
                }
                else
                {
                    var anchorX = choiceAlign == ChoiceAlign.Left ? 0f : choiceAlign == ChoiceAlign.Center ? 0.5f : 1f;
                    rect.anchorMin = new Vector2(anchorX, 1f);
                    rect.anchorMax = new Vector2(anchorX, 1f);
                    rect.pivot = new Vector2(anchorX, 1f);
                    rect.sizeDelta = size;
                }

                rect.anchoredPosition = new Vector2(insetX, -offsetY);
                offsetY += size.y + choiceButtonSpacing;
            }

            // 4) 固定尺寸的容器才跟着数量改高度；全屏拉伸的容器不动。
            if (!isStretched)
            {
                choiceRoot.sizeDelta = new Vector2(choiceRoot.sizeDelta.x, visibleCount > 0 ? stackHeight : 0f);
            }
        }

        /// <summary>算出选项文字该多大：显式设置优先，否则按高度基准 ×0.42；都不设返回 0（不动预制体字号）。</summary>
        private float ResolveChoiceFontSize()
        {
            if (choiceFontSize > 0f)
            {
                return choiceFontSize;
            }

            return choiceButtonHeight > 0f ? Mathf.Max(1f, choiceButtonHeight) * 0.42f : 0f;
        }

        /// <summary>把算出的字号应用到所有选项文字（在量文字宽之前调用）。</summary>
        private void ApplyChoiceFontSize(int visibleCount)
        {
            var size = ResolveChoiceFontSize();
            if (size <= 0f)
            {
                return;
            }

            for (var i = 0; i < visibleCount; i++)
            {
                var label = choiceButtonPool[i].Label;
                if (label != null)
                {
                    label.fontSize = size;
                }
            }
        }

        /// <summary>把这次布局的实际数字打出来，方便核对自适应是否生效。</summary>
        private void LogChoiceLayout(int visibleCount, Vector2[] sizes)
        {
            var report = $"[VN] 选项布局（模式 {choiceWidthMode}，下限 {choiceMinWidth}，留白 {choiceTextPadding}）：";
            for (var i = 0; i < visibleCount; i++)
            {
                var label = choiceButtonPool[i].Label;
                var text = label != null ? label.text : "-";
                var textWidth = label != null && !string.IsNullOrEmpty(label.text)
                    ? label.GetPreferredValues(label.text).x
                    : 0f;
                var padding = LabelPadding(label != null ? label.rectTransform : null);
                report += $"\n  [{i}] \"{text}\" 文字宽={textWidth:0} 内边距={padding.x:0}/{padding.y:0} " +
                          $"→ 按钮 {sizes[i].x:0}×{sizes[i].y:0}";
            }

            Debug.Log(report);
        }

        /// <summary>按 choiceWidthMode 把按钮改成"适应文字"：先定宽度，再按最终宽度量高度（长文本换行后按钮会自动变高）。</summary>
        private void ApplyAdaptiveWidths(int visibleCount, Vector2[] sizes)
        {
            if (choiceWidthMode == ChoiceWidthMode.Fixed)
            {
                return;
            }

            var minWidth = Mathf.Max(1f, choiceMinWidth);
            var maxWidth = Mathf.Max(minWidth, choiceMaxWidth);

            for (var i = 0; i < visibleCount; i++)
            {
                var width = MeasureChoiceWidth(choiceButtonPool[i].Label);
                if (width > 0f)
                {
                    sizes[i].x = Mathf.Clamp(width, minWidth, maxWidth);
                }
            }

            if (choiceWidthMode == ChoiceWidthMode.UniformFitText)
            {
                // 统一成最长的那条，整组看起来整齐（柚子社那种居中竖排）。
                var uniform = 0f;
                for (var i = 0; i < visibleCount; i++)
                {
                    uniform = Mathf.Max(uniform, sizes[i].x);
                }

                for (var i = 0; i < visibleCount; i++)
                {
                    sizes[i].x = uniform;
                }
            }

            // 高度：按最终宽度量一次，需要多高就给多高（文字不会溢出底板）。
            for (var i = 0; i < visibleCount; i++)
            {
                var label = choiceButtonPool[i].Label;
                if (label == null || string.IsNullOrEmpty(label.text))
                {
                    continue;
                }

                var padding = LabelPadding(label.rectTransform);
                var availableWidth = Mathf.Max(1f, sizes[i].x - padding.x);
                var neededHeight = label.GetPreferredValues(label.text, availableWidth, 0f).y + padding.y;
                if (neededHeight > sizes[i].y)
                {
                    sizes[i].y = neededHeight;
                }
            }
        }

        /// <summary>量一颗按钮需要多宽：文字宽 + 文字层自身内边距 + choiceTextPadding。</summary>
        private float MeasureChoiceWidth(TextMeshProUGUI label)
        {
            if (label == null || string.IsNullOrEmpty(label.text))
            {
                return 0f;
            }

            var textWidth = label.GetPreferredValues(label.text).x;
            var padding = LabelPadding(label.rectTransform);
            return textWidth + padding.x + Mathf.Max(0f, choiceTextPadding);
        }

        /// <summary>拉伸布局下，offsetMin/offsetMax 就是文字层的左右与上下内边距。</summary>
        private static Vector2 LabelPadding(RectTransform rect)
        {
            if (rect == null || rect.anchorMin == rect.anchorMax)
            {
                return Vector2.zero;
            }

            return new Vector2(
                Mathf.Max(0f, rect.offsetMin.x - rect.offsetMax.x),
                Mathf.Max(0f, rect.offsetMin.y - rect.offsetMax.y));
        }

        /// <summary>按钮尺寸优先级：LayoutElement 偏好值 → 底板图原始像素 → 代码兜底值（宽度 0 = 拉满容器）。</summary>
        private Vector2 ResolveButtonSize(Button button, RectTransform rect)
        {
            var fullWidth = false;
            var width = 0f;
            var height = 0f;

            var layoutElement = button.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                // flexibleWidth > 0 表示"横向拉满容器"（九宫格底板的用法）。
                fullWidth = layoutElement.flexibleWidth > 0f;
                width = layoutElement.preferredWidth > 0f ? layoutElement.preferredWidth : 0f;
                height = layoutElement.preferredHeight > 0f ? layoutElement.preferredHeight : 0f;
            }

            if (!fullWidth && (width <= 0f || height <= 0f))
            {
                var image = button.targetGraphic as Image;
                if (image == null)
                {
                    image = button.GetComponent<Image>();
                }

                if (image != null && image.sprite != null && image.sprite.border == Vector4.zero)
                {
                    // 只有"不可拉伸"的整图底板才用它的原始像素当按钮尺寸；
                    // 九宫格底板的原始尺寸只是切片尺寸，尺寸交给 LayoutElement / 兜底值。
                    if (width <= 0f)
                    {
                        width = image.sprite.rect.width;
                    }

                    if (height <= 0f)
                    {
                        height = image.sprite.rect.height;
                    }
                }
            }

            if (choiceButtonHeight > 0f)
            {
                // 显式指定了高度基准：就按它来（预制体的高度只作美术参考）。
                height = choiceButtonHeight;
            }
            else if (height <= 0f)
            {
                height = rect.sizeDelta.y > 0f ? rect.sizeDelta.y : 48f;
            }

            if (fullWidth)
            {
                width = 0f;
            }
            else if (width <= 0f)
            {
                width = choiceButtonWidth;
            }

            return new Vector2(width, height);
        }

        private void SelectChoice(int choiceIndex)
        {
            if (choiceIndex < 0 || choiceIndex >= currentChoiceIds.Count)
            {
                return;
            }

            var choiceId = currentChoiceIds[choiceIndex];
            if (!string.IsNullOrWhiteSpace(choiceId) && Services.TryGet<UIManager>(out var uiManager))
            {
                HideChoices();
                uiManager.SelectVNChoice(choiceId);
            }
        }
    }
}
