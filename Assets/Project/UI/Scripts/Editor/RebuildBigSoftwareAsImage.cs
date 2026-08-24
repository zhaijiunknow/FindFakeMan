using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Project.UI.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Panels.Editor
{
    /// <summary>
    /// 按「Canvas 里那张大软件参考图 (Image)」的样式，把 BigSoftware 重新拼成完整的
    /// Electronic Control System 布局（标题栏 + 主面板 + 右栏 Health/Collect + 底部 Items + 右雷达），
    /// 尺寸按实际 Canvas 参考分辨率（如 800×600）。BigApp.prefab 只有 game/nothink/leida 简化块，
    /// 这里用「UI/大软件」素材还原完整界面。
    /// 用法：Tools → Panel Stack → Rebuild BigSoftware As Image。
    /// </summary>
    public static class RebuildBigSoftwareAsImage
    {
        [MenuItem("Tools/Panel Stack/Rebuild BigSoftware As Image")]
        public static void Rebuild()
        {
            var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Rebuild] 未找到 Canvas。");
                return;
            }

            var scaler = canvas.GetComponent<CanvasScaler>();
            var sz = scaler != null ? scaler.referenceResolution : new Vector2(800f, 600f);
            if (sz.x <= 1f || sz.y <= 1f) sz = new Vector2(800f, 600f);

            var big = GameObject.Find("BigSoftware");
            if (big == null)
            {
                // 若不存在则建到 Canvas 下。
                big = new GameObject("BigSoftware", typeof(RectTransform));
                big.transform.SetParent(canvas.transform, false);
            }

            // 清空旧子元素，按参考图样式重建。
            foreach (Transform child in big.transform.Cast<Transform>().ToArray())
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            // 确保有 PanelBase(SimplePanel) + CanvasGroup。
            if (big.GetComponent<SimplePanel>() == null) big.AddComponent<SimplePanel>();
            if (big.GetComponent<CanvasGroup>() == null) big.AddComponent<CanvasGroup>();
            var panel = big.GetComponent<SimplePanel>();
            var pso = new SerializedObject(panel);
            pso.FindProperty("panelId").stringValue = "main_menu";
            pso.FindProperty("kind").enumValueIndex = (int)PanelKind.Page;
            pso.ApplyModifiedPropertiesWithoutUndo();

            var root = (RectTransform)big.transform;
            Stretch(root, canvas.transform);

            Func<string, Sprite> sp = n => LoadSprite($"Assets/Project/Resource/UI/大软件/{n}");

            // 背景：big_background 铺满。
            var bg = Img(root, "BackGround", sp("big_background.png"), 0f, 0f, 1f, 1f, sz);
            Stretch((RectTransform)bg.transform, root);

            // 标题栏：红条 + 标题 + 右上三圆点（坐标按参考图实测）。
            Solid(root, "TitleBarAccent", new Color(0.85f, 0.25f, 0.2f), 0.034f, 0.024f, 0.033f, 0.017f, sz);
            Txt(root, "TitleText", "Electronic Control System", 0.045f, 0.012f, 0.50f, 0.045f, sz, 28, TextAnchor.MiddleLeft);
            Solid(root, "DotB", new Color(0.35f, 0.55f, 0.9f), 0.944f, 0.021f, 0.013f, 0.022f, sz);
            Solid(root, "DotG", new Color(0.3f, 0.75f, 0.45f), 0.960f, 0.021f, 0.013f, 0.022f, sz);
            Solid(root, "DotR", new Color(0.85f, 0.2f, 0.2f), 0.975f, 0.021f, 0.013f, 0.022f, sz);

            // 主面板：大灰块。
            Solid(root, "MainPanel", new Color(64f / 255f, 65f / 255f, 71f / 255f), 0.033f, 0.030f, 0.837f, 0.720f, sz);

            // 右栏。
            Solid(root, "SidePanel", new Color(52f / 255f, 54f / 255f, 60f / 255f), 0.886f, 0.030f, 0.094f, 0.720f, sz);
            Txt(root, "HealthLabel", "Health", 0.886f, 0.075f, 0.094f, 0.035f, sz, 24, TextAnchor.MiddleCenter);
            Img(root, "HealthHearts", sp("life_background.png"), 0.890f, 0.093f, 0.064f, 0.230f, sz);
            Txt(root, "CollectLabel", "Collect", 0.880f, 0.345f, 0.094f, 0.035f, sz, 24, TextAnchor.MiddleCenter);
            for (int i = 0; i < 3; i++)
                Img(root, $"CollectSlot{i}", sp(i == 0 ? "box_select.png" : "box.png"), 0.892f, 0.385f + i * 0.105f, 0.064f, 0.09f, sz);

            // 底部：左块 + Items 4 槽 + 右雷达。
            Solid(root, "BottomLeft", new Color(52f / 255f, 54f / 255f, 60f / 255f), 0.034f, 0.740f, 0.275f, 0.223f, sz);
            Txt(root, "ItemsLabel", "Items", 0.330f, 0.875f, 0.08f, 0.04f, sz, 26, TextAnchor.MiddleLeft);
            for (int i = 0; i < 4; i++)
                Img(root, $"ItemSlot{i}", sp(i == 0 ? "item_select.png" : "item.png"), 0.34f + i * 0.145f, 0.755f, 0.11f, 0.18f, sz);

            var leida = Img(root, "LeidaBack", sp("leida_back.png"), 0.866f, 0.768f, 0.111f, 0.195f, sz);
            var leidaInner = Img(leida.transform, "Leida", sp("leida.png"), 0f, 0f, 1f, 1f, sz);
            Stretch((RectTransform)leidaInner.transform, leida.transform);

            // 设置按钮。
            Btn(root, "BtnSettings", "设置", 0.86f, 0.905f, 0.09f, 0.05f, sz, "settings");

            EditorUtility.SetDirty(big);
            EditorUtility.SetDirty(canvas.gameObject);
            Debug.Log($"[Rebuild] BigSoftware 已按大软件参考图样式重建，画布 {sz.x}×{sz.y}。");
        }

        // ---------- helpers（归一化坐标 nx/ny 左上原点 × 画布尺寸 sz） ----------

        private static Image Img(Transform parent, string name, Sprite sprite, float nx, float ny, float nw, float nh, Vector2 sz)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            CenterByRect(r, nx, ny, nw, nh, sz);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = Color.white;
            return img;
        }

        private static Image Solid(Transform parent, string name, Color color, float nx, float ny, float nw, float nh, Vector2 sz)
        {
            var img = Img(parent, name, null, nx, ny, nw, nh, sz);
            img.color = color;
            return img;
        }

        private static Text Txt(Transform parent, string name, string text, float nx, float ny, float nw, float nh, Vector2 sz, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            CenterByRect(r, nx, ny, nw, nh, sz);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = anchor;
            t.color = Color.white;
            return t;
        }

        private static void Btn(Transform parent, string name, string text, float nx, float ny, float nw, float nh, Vector2 sz, string openId)
        {
            var img = Img(parent, name, LoadSprite("Assets/Project/Resource/UI/大软件/button.png"), nx, ny, nw, nh, sz);
            var b = img.gameObject.AddComponent<Button>();
            var label = Txt(img.transform, "Label", text, 0f, 0f, 1f, 1f, sz, Mathf.RoundToInt(sz.y * 0.07f), TextAnchor.MiddleCenter);
            Stretch((RectTransform)label.transform, img.transform);

            var action = img.gameObject.AddComponent<ButtonAction>();
            var so = new SerializedObject(action);
            so.FindProperty("actionType").enumValueIndex = (int)ButtonAction.ActionType.OpenPanel;
            so.FindProperty("targetId").stringValue = openId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CenterByRect(RectTransform r, float nx, float ny, float nw, float nh, Vector2 sz)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2((nx + nw * 0.5f - 0.5f) * sz.x, (0.5f - (ny + nh * 0.5f)) * sz.y);
            r.sizeDelta = new Vector2(nw * sz.x, nh * sz.y);
        }

        private static void Stretch(RectTransform rect, Transform parent)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite LoadSprite(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) return null;
            s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            s.name = $"{System.IO.Path.GetFileNameWithoutExtension(path)}_generated";
            return s;
        }
    }
}
