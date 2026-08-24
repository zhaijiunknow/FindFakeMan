using System.Linq;
using Cysharp.Threading.Tasks;
using Project.UI.Scripts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.UI.Panels.Editor
{
    /// <summary>
    /// 一键搭面板栈验证场景。
    ///
    /// 用法：Tools → Panel Stack → Build Validation Scene。
    /// 生成 PanelManager（含注册表）+ PanelUIRoot + 三个面板 prefab（SimplePanel）：
    ///   - MainMenuPage (Page)     main_menu
    ///   - LevelSelectPage (Page)  level_select
    ///   - SettingsOverlay (Overlay) settings
    /// 场景保存到 Assets/Project/UI/Scenes/PanelValidation.unity。
    /// </summary>
    public static class PanelValidationBuilder
    {
        private const string SavePath = "Assets/Project/UI/Scenes/PanelValidation.unity";

        [MenuItem("Tools/Panel Stack/Build Validation Scene")]
        public static void Build()
        {
            // 新建未被保存过的空场景，才能 SaveScene 到目标路径。
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var all = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in all)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // ---------- PanelManager ----------
            var pmGo = new GameObject("PanelManager");
            var pm = pmGo.AddComponent<PanelManager>();
            var pmSo = new SerializedObject(pm);
            pmSo.FindProperty("handleEscape").boolValue = true;
            pmSo.FindProperty("clearStackOnSceneLoad").boolValue = true;

            // ---------- 面板 prefab ----------
            var mainMenu = CreateBigSoftwarePage("main_menu");
            var levelSelect = CreatePagePrefab("LevelSelectPage", "level_select", "选关页面", null, null);
            var settings = CreateOverlayPrefab("SettingsOverlay", "settings");

            // ---------- 注册表 ----------
            var panels = pmSo.FindProperty("panels");
            panels.arraySize = 3;
            SetEntry(panels.GetArrayElementAtIndex(0), "main_menu", mainMenu, PanelKind.Page);
            SetEntry(panels.GetArrayElementAtIndex(1), "level_select", levelSelect, PanelKind.Page);
            SetEntry(panels.GetArrayElementAtIndex(2), "settings", settings, PanelKind.Overlay);
            pmSo.ApplyModifiedPropertiesWithoutUndo();

            // ---------- 初始打开主菜单 ----------
            var starter = pmGo.AddComponent<OpenMainMenuOnStart>();
            var starterSo = new SerializedObject(starter);
            starterSo.FindProperty("manager").objectReferenceValue = pm;
            starterSo.ApplyModifiedPropertiesWithoutUndo();

            // ---------- 保存 ----------
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), SavePath);
            Debug.Log($"[PanelValidation] 已验证场景生成：{SavePath}");
        }

        private static void SetEntry(SerializedProperty entry, string id, GameObject prefab, PanelKind kind)
        {
            entry.FindPropertyRelative("panelId").stringValue = id;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("kind").enumValueIndex = (int)kind;
        }

        private const float RefW = 1920f;
        private const float RefH = 1080f;

        /// <summary>
        /// 大软件 Page 面板：按 BigApp.prefab 的样式用「UI/大软件」素材重新拼，
        /// 尺寸换算成 1920×1080（BigApp 是 PSD 像素坐标 3649×2160，整体缩放会错位）。
        /// 布局：big_background 铺底 + game 主面板 + nothink 左下块 + leida_back/leida 右下雷达。
        /// 作为固定背景（不挂 UIWindowManager）。
        /// </summary>
        private static GameObject CreateBigSoftwarePage(string id)
        {
            var go = new GameObject("BigSoftware", typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup));
            StretchRect((RectTransform)go.transform);

            var panel = go.AddComponent<SimplePanel>();
            var pso = new SerializedObject(panel);
            pso.FindProperty("panelId").stringValue = id;
            pso.FindProperty("kind").enumValueIndex = (int)PanelKind.Page;
            pso.ApplyModifiedPropertiesWithoutUndo();

            // 背景铺满深色底。
            var bg = AddSprite(go.transform, "BackGround", LoadSprite(Dir("big_background.png")), 0f, 0f, 1f, 1f);
            var bgRect = (RectTransform)bg.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // game 主面板（中上大块）。
            AddSprite(go.transform, "Game", LoadSprite(Dir("game.png")), -0.069f, 0.148f, 0.823f, 0.72f);
            // nothink 左下块。
            AddSprite(go.transform, "Nothink", LoadSprite(Dir("nothink.png")), -0.067f, 0.887f, 0.254f, 0.206f);
            // leida 右下雷达（leida_back 底 + leida.png 前景）。
            var leida = AddSprite(go.transform, "LeidaBack", LoadSprite(Dir("leida_back.png")), 0.768f, 0.885f, 0.111f, 0.206f);
            AddSprite(leida.transform, "Leida", LoadSprite(Dir("leida.png")), 0f, 0f, 1f, 1f);

            // 设置按钮叠右上，供面板栈开关验证。
            AddButtonAt(go.transform, "BtnSettings", "设置", 0.90f, 0.905f, 0.09f, 0.05f,
                ButtonAction.ActionType.OpenPanel, "settings");

            return go;
        }

        private static string Dir(string file) => $"Assets/Project/Resource/UI/大软件/{file}";

        /// <summary>归一化矩形 → 中心锚点的等宽 Image（无贴图，纯色）。</summary>
        private static Image AddSolid(Transform parent, string name, Color color,
            float nx, float ny, float nw, float nh)
        {
            var img = AddSprite(parent, name, null, nx, ny, nw, nh);
            img.sprite = null;
            img.color = color;
            return img;
        }

        /// <summary>归一化矩形 → 中心锚点的 Image。</summary>
        private static Image AddSprite(Transform parent, string name, Sprite sprite,
            float nx, float ny, float nw, float nh)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            CenterByRect(r, nx, ny, nw, nh);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = Color.white;
            return img;
        }

        private static Text AddText(Transform parent, string name, string text,
            float nx, float ny, float nw, float nh, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            CenterByRect(r, nx, ny, nw, nh);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = anchor;
            t.color = Color.white;
            return t;
        }

        private static void AddButtonAt(Transform parent, string name, string text,
            float nx, float ny, float nw, float nh, ButtonAction.ActionType type, string id)
        {
            var btn = AddSprite(parent, name, LoadSprite(Dir("button.png")), nx, ny, nw, nh);
            var go = btn.gameObject;
            var b = go.AddComponent<Button>();
            b.targetGraphic = btn;
            var label = AddText(go.transform, "Label", text, 0f, 0f, 1f, 1f, 46, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            SetButtonAction(b, type, id);
        }

        /// <summary>以画布中心为锚点，把 RectTransform 按归一化矩形（左上原点）定位。</summary>
        private static void CenterByRect(RectTransform r, float nx, float ny, float nw, float nh)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2((nx + nw * 0.5f - 0.5f) * RefW, (0.5f - (ny + nh * 0.5f)) * RefH);
            r.sizeDelta = new Vector2(nw * RefW, nh * RefH);
        }

        private static void StretchRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogWarning($"[PanelValidation] 找不到贴图：{path}");
                return null;
            }

            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            sprite.name = $"{System.IO.Path.GetFileNameWithoutExtension(path)}_generated";
            return sprite;
        }

        private static GameObject CreatePagePrefab(string name, string id, string label,
            string openId1, string openId2)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(CanvasGroup));
            go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.2f, 1f);

            var panel = go.AddComponent<SimplePanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("panelId").stringValue = id;
            so.FindProperty("kind").enumValueIndex = (int)PanelKind.Page;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 标题
            var title = CreateText("Title", go.transform, label, new Vector2(0f, 260f), 80);

            // 按钮（用 ButtonAction 运行时驱动，避免持久监听绑定 lambda 的限制）
            if (!string.IsNullOrEmpty(openId1))
            {
                var btn = CreateButton("Open1", go.transform, "打开 "+openId1, new Vector2(0f, -40f));
                SetButtonAction(btn, ButtonAction.ActionType.OpenPanel, openId1);
            }

            if (!string.IsNullOrEmpty(openId2))
            {
                var btn = CreateButton("Open2", go.transform, "打开 "+openId2, new Vector2(0f, -180f));
                SetButtonAction(btn, ButtonAction.ActionType.OpenPanel, openId2);
            }

            // 返回/关闭
            var close = CreateButton("Close", go.transform, "关闭", new Vector2(0f, -320f));
            SetButtonAction(close, ButtonAction.ActionType.CloseTopPanel, null);

            return go;
        }

        private static GameObject CreateOverlayPrefab(string name, string id)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(CanvasGroup));
            // 全屏变暗底板：raycastTarget=true 挡住下层交互。
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            go.GetComponent<CanvasGroup>().blocksRaycasts = true;

            var panel = go.AddComponent<SimplePanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("panelId").stringValue = id;
            so.FindProperty("kind").enumValueIndex = (int)PanelKind.Overlay;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 居中窗口
            var win = new GameObject("Window", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            win.GetComponent<Image>().color = new Color(0.95f, 0.95f, 0.98f, 1f);
            var winRect = (RectTransform)win.transform;
            winRect.SetParent(go.transform, false);
            winRect.anchorMin = new Vector2(0.5f, 0.5f);
            winRect.anchorMax = new Vector2(0.5f, 0.5f);
            winRect.pivot = new Vector2(0.5f, 0.5f);
            winRect.anchoredPosition = Vector2.zero;
            winRect.sizeDelta = new Vector2(700f, 420f);

            var title = CreateText("Title", win.transform, "设置浮层", new Vector2(0f, 150f), 60);
            var close = CreateButton("Close", win.transform, "关闭", new Vector2(0f, -120f));
            SetButtonAction(close, ButtonAction.ActionType.CloseTopPanel, null);

            return go;
        }

        private static Text CreateText(string name, Transform parent, string text, Vector2 pos, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(900f, 120f);
            return t;
        }

        private static Button CreateButton(string name, Transform parent, string text, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var img = go.GetComponent<Image>();
            img.color = new Color(0.35f, 0.5f, 0.9f, 1f);
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(360f, 100f);

            var sub = CreateText("Label", rt, text, Vector2.zero, 44);
            sub.color = Color.white;

            return go.GetComponent<Button>();
        }

        /// <summary>给按钮挂 ButtonAction 组件（运行时自动绑定 Button.onClick）。</summary>
        private static void SetButtonAction(Button btn, ButtonAction.ActionType type, string id)
        {
            var action = btn.gameObject.AddComponent<ButtonAction>();
            var so = new SerializedObject(action);
            so.FindProperty("actionType").enumValueIndex = (int)type;
            so.FindProperty("targetId").stringValue = id ?? "";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
