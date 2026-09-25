using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 小软件子页的**统一视觉**（和参考图 System Setting 一套）：
    /// 左上角标题、左对齐的行标签、蓝色圆角按钮、底部居中的「返回」。
    ///
    /// 为什么要抽出来：笔记 / 线索清单 / 道具 / 收容 / 设置 这几页如果各写各的配色 ✗，
    /// 一眼就看得出是拼的 ✗；集中在这里之后，改色/改字号只动这一个文件 ✓。
    ///
    /// 用法：`SmallAppPageStyle.Title(父节点, 字体, "笔记")` 之类；字体用 `SmallAppPageStyle.ResolveFont(this)` ✓。
    /// </summary>
    public static class SmallAppPageStyle
    {
        public static readonly Color TitleColor = new Color(0.88f, 0.92f, 1f, 1f);
        public static readonly Color LabelColor = new Color(0.86f, 0.90f, 0.98f, 1f);
        public static readonly Color BodyColor = new Color(0.88f, 0.94f, 1f, 1f);
        public static readonly Color ButtonColor = new Color(0.62f, 0.72f, 0.96f, 1f);
        public static readonly Color ButtonSelectedColor = new Color(0.45f, 0.58f, 0.92f, 1f);
        public static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.92f);
        public static readonly Color FillColor = new Color(0.45f, 0.58f, 0.92f, 1f);

        /// <summary>页面标题（参考图左上角那个 System Setting 的位置与字号）。</summary>
        public static TextMeshProUGUI Title(Transform parent, TMP_FontAsset font, string content)
        {
            return Label(parent, "Title", content, font, 0.05f, 0.95f, 0.86f, 0.96f, 30, TitleColor, TextAlignmentOptions.Left);
        }

        /// <summary>正文（多行文本，铺在标题下方）。返回的 TextMeshProUGUI 由调用方按需刷新文字 ✓。</summary>
        public static TextMeshProUGUI Body(Transform parent, TMP_FontAsset font, string content)
        {
            return Label(parent, "Body", content, font, 0.05f, 0.95f, 0.20f, 0.84f, 22, BodyColor, TextAlignmentOptions.TopLeft);
        }

        /// <summary>行标签（如"显示模式""主音量"）。</summary>
        public static void RowLabel(Transform parent, string name, string content, TMP_FontAsset font, float yMin, float yMax)
        {
            Label(parent, name, content, font, 0.10f, 0.32f, yMin, yMax, 26, LabelColor, TextAlignmentOptions.Left);
        }

        /// <summary>
        /// 小节标题（左对齐小字 ✓，比如「道具」页的「背包」「工具包」✓）。
        /// 默认整行铺满 ✓，需要并排两列时传 xMin / xMax 分栏 ✓。
        /// </summary>
        public static TextMeshProUGUI Caption(Transform parent, string name, string content, TMP_FontAsset font,
            float yMin, float yMax, float xMin = 0.05f, float xMax = 0.95f)
        {
            return Label(parent, name, content, font, xMin, xMax, yMin, yMax, 20, TitleColor, TextAlignmentOptions.Left);
        }

        /// <summary>蓝色圆角按钮（选中态用 <see cref="SetSelected"/> 换色 ✓）。</summary>
        public static Button Choice(Transform parent, string name, string label, TMP_FontAsset font,
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
            image.color = ButtonColor;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var text = Label(rect, "Label", label, font, 0f, 1f, 0f, 1f, 24, Color.white, TextAlignmentOptions.Center);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        /// <summary>底部居中的「返回」（子页返回 = 回到分页菜单 ✓）。<paramref name="onClick"/> 可以为 null ✓ ——
        /// 那时只建按钮、不接线（接线留给各页的 <c>WireEvents</c> 做 ✓，因为编辑期加的监听不会被序列化 ✗）。</summary>
        public static Button BackButton(Transform parent, TMP_FontAsset font, UnityAction onClick)
        {
            var button = Choice(parent, "Choice_Back", "返回", font, 0.38f, 0.62f, 0.06f, 0.17f);
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            return button;
        }

        /// <summary>
        /// 设置按钮的"选中"外观。
        /// **默认什么都不做** ✗→✓：选中态交给按钮自己做（精灵切换 / Transition ✓），
        /// 这里改 `Image.color` 会把 sprite 染色/压暗，正好和美术那套冲突 ✗。
        /// 只有确实没有选中态表现的按钮，才传 <paramref name="tintIfNoSprite"/> = true 用颜色顶一下 ✓。
        /// </summary>
        public static void SetSelected(Button button, bool selected, bool tintIfNoSprite = false)
        {
            if (!tintIfNoSprite || button == null || !(button.targetGraphic is Image image))
            {
                return;
            }

            image.color = selected ? ButtonSelectedColor : ButtonColor;
        }

        /// <summary>借场景里已有的中文字体（小软件自带一堆 TMP 文本 ✓），避免中文变豆腐块 ✗。</summary>
        public static TMP_FontAsset ResolveFont(MonoBehaviour context)
        {
            if (context == null)
            {
                return TMP_Settings.defaultFontAsset;
            }

            var canvas = context.GetComponentInParent<Canvas>(true);
            var host = canvas != null ? canvas.transform : context.transform.root;
            foreach (var text in host.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text != null && text.font != null)
                {
                    return text.font;
                }
            }

            return TMP_Settings.defaultFontAsset;
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
    }
}
