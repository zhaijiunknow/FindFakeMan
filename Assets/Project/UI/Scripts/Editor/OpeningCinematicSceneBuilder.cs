using System;
using System.Linq;
using Project.Core.Runtime.Managers;
using Project.Narrative.Scripts;
using Project.UI.Panels;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.UI.OpeningCinematic.Editor
{
    /// <summary>
    /// 一键搭建开场动画原型场景。
    ///
    /// 用法：Tools → Opening Cinematic → Build Prototype Scene。
    /// 会清空当前活动场景并重建：电影相机 + 捕获相机 + EventSystem + 菜单画布(Screen Space Camera)
    /// + 房间背景 quad + 显示器 quad（运行时贴菜单 RT）+ 锚点 + CinematicOpeningDirector，最后保存。
    ///
    /// 设计数值只是起点：把 startAnchor / endAnchor 在场景里拖到想要的位置即可重摆推镜路径。
    /// </summary>
    public static class OpeningCinematicSceneBuilder
    {
        private const string RoomImagePath = "Assets/Project/Resource/Indoor/扣a1.png";
        private const string UiBackgroundPath = "Assets/Project/Resource/UI/大软件/大软件.png";
        private const string StartButtonPath = "Assets/Project/Resource/UI/button_start.png";
        private const string SettingsButtonPath = "Assets/Project/Resource/UI/button_setting.png";
        private const string SavePath = "Assets/Project/UI/Scenes/OpeningCinematic.unity";

        private const int UiLayer = 5;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        [MenuItem("Tools/Opening Cinematic/Build Prototype Scene")]
        public static void Build()
        {
            ClearScene();
            BuildScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), SavePath);
            Debug.Log($"[OpeningCinematic] 原型场景已生成并保存：{SavePath}");
        }

        private static void ClearScene()
        {
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in all)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void BuildScene()
        {
            // ---------- 相机 ----------
            var mainCam = CreateCamera(
                "Main Camera", new Vector3(0f, 1.1f, 20.5f), Quaternion.identity, isMain: true,
                clearColor: new Color(0.02f, 0.02f, 0.03f), uiLayerExcluded: true);
            mainCam.fieldOfView = 60f;

            var captureCam = CreateCamera(
                "UI Capture Camera", new Vector3(0f, 1.1f, -1f), Quaternion.identity, isMain: false,
                clearColor: Color.black, uiLayerOnly: true);

            // ---------- 事件系统（开场期间由 Director 禁用） ----------
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // ---------- 景：房间背景(扣a1) + 显示器 quad ----------
            // 背景 quad 铺满；显示器 quad 对齐到扣a1里显示器屏幕的位置（用遮罩1蓝块定位）。
            // quad 法线朝相机(+Z)以保持贴图不镜像；相机本身不旋转（全程朝 +Z）。
            var roomQuad = CreateQuad(
                "Room Background", new Vector3(0f, 1.1f, 40f), new Vector3(40f, 22.5f, 1f),
                Quaternion.identity);
            var roomMaterial = CreateUnlitMaterial("RoomMat", LoadTexture(RoomImagePath));
            roomQuad.GetComponent<MeshRenderer>().sharedMaterial = roomMaterial;

            // 显示器屏幕中心(遮罩1蓝块，扣a1坐标系)：水平0.5499 垂直0.4773
            // 映射到背景quad(40×22.5 @ (0,1.1,40)) ≈ 世界(2.0, 1.61)，宽 ≈ 3.70。
            // 高按 16:9 取 2.08（与 UI 画布 1920×1080 同比例），避免 UI 横向压扁。
            var monitorQuad = CreateQuad(
                "Monitor", new Vector3(2.0f, 1.61f, 40f), new Vector3(3.70f, 2.08f, 1f),
                Quaternion.identity);
            var monitorRenderer = monitorQuad.GetComponent<MeshRenderer>();

            // ---------- 锚点 ----------
            // 显示器 quad 与背景同 z=40（贴合背景平面，由材质 ZTestAlways 稳定盖在背景上）。
            // 相机全程在显示器(z=40)前方朝 +Z 看（rotation≈0），永不穿过 → 不会翻转。
            // 起点对准背景中心(0,1.1)：背景 40×22.5@z40，相机起点 z=20.5 距离 19.5 → 视口 22.5×40，背景正好填满全屏。
            // 终点对准屏幕：x/y=屏幕中心(2,1.61)，z=38.2（距屏幕 1.8）→ 屏幕充满。注视点从背景中心插值到屏幕中心。
            var anchorRoot = new GameObject("Anchors");
            var startAnchor = CreateAnchor(anchorRoot.transform, "StartAnchor", new Vector3(0f, 1.1f, 20.5f));
            var endAnchor = CreateAnchor(anchorRoot.transform, "EndAnchor", new Vector3(2.0f, 1.61f, 38.2f));
            var lookTarget = CreateAnchor(anchorRoot.transform, "LookTarget", new Vector3(2.0f, 1.61f, 40f));

            // ---------- 黑边画布（Overlay，推镜时上下移入） ----------
            CreateLetterbox(out var letterboxTop, out var letterboxBottom);

            // ---------- 面板栈：PanelUIRoot(ScreenSpaceCamera) + PanelManager + 面板注册 ----------
            var panelUIRoot = CreatePanelRoot(captureCam);
            var panelManagerGo = new GameObject("PanelManager");
            var panelManager = panelManagerGo.AddComponent<PanelManager>();
            var pms = new SerializedObject(panelManager);
            pms.FindProperty("uiRoot").objectReferenceValue = (RectTransform)panelUIRoot.transform;
            SetupPanels(pms);
            pms.ApplyModifiedPropertiesWithoutUndo();

            // ---------- 运行 manager（支撑 start 序章 / setting_icon / 槽位） ----------
            CreateManagers();

            // ---------- 导演 ----------
            var directorGo = new GameObject("CinematicOpeningDirector");
            var director = directorGo.AddComponent<Project.UI.OpeningCinematic.CinematicOpeningDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("cinematicCamera").objectReferenceValue = mainCam;
            so.FindProperty("uiCaptureCamera").objectReferenceValue = captureCam;
            so.FindProperty("startAnchor").objectReferenceValue = startAnchor;
            so.FindProperty("endAnchor").objectReferenceValue = endAnchor;
            so.FindProperty("lookTarget").objectReferenceValue = lookTarget;
            so.FindProperty("monitorRenderer").objectReferenceValue = monitorRenderer;
            so.FindProperty("backgroundRenderer").objectReferenceValue = roomQuad.GetComponent<MeshRenderer>();
            so.FindProperty("panelCanvas").objectReferenceValue = panelUIRoot.GetComponent<Canvas>();
            so.FindProperty("eventSystem").objectReferenceValue = eventSystemGo.GetComponent<EventSystem>();
            so.FindProperty("letterboxTopBar").objectReferenceValue = letterboxTop;
            so.FindProperty("letterboxBottomBar").objectReferenceValue = letterboxBottom;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- 辅助 ----------

        private static Camera CreateCamera(
            string name, Vector3 position, Quaternion rotation,
            bool isMain, Color clearColor, bool uiLayerOnly = false, bool uiLayerExcluded = false)
        {
            var go = new GameObject(name, typeof(Camera));
            var cam = go.GetComponent<Camera>();

            cam.transform.position = position;
            cam.transform.rotation = rotation;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = clearColor;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.depth = uiLayerOnly ? -1 : 0;

            var mask = uiLayerOnly
                ? 1 << UiLayer
                : uiLayerExcluded
                    ? ~0 & ~(1 << UiLayer)
                    : ~0;
            cam.cullingMask = mask;

            if (isMain)
            {
                go.tag = "MainCamera";
                if (go.GetComponent<AudioListener>() == null)
                {
                    go.AddComponent<AudioListener>();
                }
            }
            else
            {
                // 捕获相机只渲 UI，不需要监听器。
                var listener = go.GetComponent<AudioListener>();
                if (listener != null)
                {
                    UnityEngine.Object.DestroyImmediate(listener);
                }
            }

            return cam;
        }

        private static GameObject CreateQuad(string name, Vector3 position, Vector3 scale, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            if (rotation.HasValue)
            {
                go.transform.rotation = rotation.Value;
            }

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            return go;
        }

        private static Material CreateUnlitMaterial(string name, Texture texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Texture");
            var material = new Material(shader) { name = name };
            if (texture != null)
            {
                material.SetTexture(BaseMap, texture);
            }
            // 2.5D 广告牌双面可见：关掉背面剔除，避免朝向判断出错时整块消失。
            material.SetFloat("_Cull", 0f);
            return material;
        }

        private static Texture2D LoadTexture(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Sprite LoadSprite(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogWarning($"[OpeningCinematic] 找不到贴图：{path}");
                return null;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                sprite = Sprite.Create(
                    texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                sprite.name = $"{System.IO.Path.GetFileNameWithoutExtension(path)}_generated";
            }
            return sprite;
        }

        private static Transform CreateAnchor(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go.transform;
        }

        private static Canvas CreateMenuCanvas(Camera captureCam, Transform eventSystemParent)
        {
            var canvasGo = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = UiLayer;

            var rect = (RectTransform)canvasGo.transform;
            SetStretch(rect);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = captureCam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 10;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var root = (RectTransform)canvasGo.transform;

            // 桌面背景
            var background = CreateUiImage("DesktopBackground", root, new Rect(0f, 0f, 1920f, 1080f));
            SetStretch(background.rectTransform);
            background.sprite = LoadSprite(UiBackgroundPath);
            background.color = Color.white;

            // 标题
            var titleRect = CreateRect("Title", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 260f),
                new Vector2(1200f, 120f));
            var title = titleRect.gameObject.AddComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.text = "FIND FAKE MAN";
            title.fontSize = 72;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;
            titleRect.gameObject.AddComponent<CanvasRenderer>();

            // 开始 / 设置 按钮
            var handlerRoot = new GameObject("MenuHandler");
            handlerRoot.transform.SetParent(canvasGo.transform, false);
            handlerRoot.layer = UiLayer;
            var handler = handlerRoot.AddComponent<PrototypeMenuHandler>();

            var startButton = CreateUiButton("StartButton", root, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -100f), new Vector2(360f, 120f), LoadSprite(StartButtonPath));
            UnityEventTools.AddVoidPersistentListener(startButton.onClick, handler.OnStartClicked);

            var settingsButton = CreateUiButton("SettingsButton", root, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -260f), new Vector2(360f, 120f), LoadSprite(SettingsButtonPath));
            UnityEventTools.AddVoidPersistentListener(settingsButton.onClick, handler.OnSettingsClicked);

            // 信息面板（点开始切换，验证可交互）
            var infoPanel = CreateUiImage("InfoPanel", root, new Rect(0f, 0f, 600f, 120f));
            infoPanel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            infoPanel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            infoPanel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            infoPanel.rectTransform.anchoredPosition = new Vector2(0f, -420f);
            infoPanel.rectTransform.sizeDelta = new Vector2(600f, 120f);
            infoPanel.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            infoPanel.gameObject.SetActive(false);

            var handlerSo = new SerializedObject(handler);
            handlerSo.FindProperty("infoPanel").objectReferenceValue = infoPanel.gameObject;
            handlerSo.ApplyModifiedPropertiesWithoutUndo();

            return canvas;
        }

        /// <summary>
        /// 面板栈画布：ScreenSpace-Camera（由捕获相机渲进 RT → 显示器），供开屏推镜阶段 UI 只在屏幕上。
        /// 面板（Main/SmallApp）由 PanelManager 实例化为其子物体。
        /// </summary>
        private static GameObject CreatePanelRoot(Camera captureCam)
        {
            var go = new GameObject("PanelUIRoot", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = UiLayer;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = captureCam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 10;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            SetStretch((RectTransform)go.transform);
            return go;
        }

        private static void SetupPanels(SerializedObject pms)
        {
            var panels = pms.FindProperty("panels");
            panels.arraySize = 2;
            SetEntry(panels.GetArrayElementAtIndex(0), "main", LoadPrefab("Main.prefab"), PanelKind.Page);
            SetEntry(panels.GetArrayElementAtIndex(1), "SmallApp", LoadPrefab("SmallApp.prefab"), PanelKind.Page);
        }

        private static void SetEntry(SerializedProperty entry, string id, GameObject prefab, PanelKind kind)
        {
            entry.FindPropertyRelative("panelId").stringValue = id;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("kind").enumValueIndex = (int)kind;
        }

        private static GameObject LoadPrefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/UI/Prefabs/" + name);

        private static void CreateManagers()
        {
            NewManager<GameManager>("GameManager");
            NewManager<FlagManager>("FlagManager");
            NewManager<UIManager>("UIManager");
            NewManager<InventoryManager>("InventoryManager");
            NewManager<VNDirector>("VNDirector");
        }

        private static void NewManager<T>(string name) where T : Component
        {
            new GameObject(name).AddComponent<T>();
        }

        /// <summary>
        /// 创建 Screen Space Overlay 画布 + 上下两条黑边。黑边初始放在屏幕外（上下缘之外），
        /// 由 Director.letterboxOffset 驱动移入。raycastTarget=false 避免挡住按钮点击。
        /// </summary>
        private static void CreateLetterbox(out RectTransform topBar, out RectTransform bottomBar)
        {
            const float offset = 150f; // 与 Director.letterboxOffset 默认一致（每条黑边高度/移入距离）
            const float width = 1920f;
            const float height = 1080f;

            var canvasGo = new GameObject("LetterboxCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // 盖在最上（含菜单 RT 画面）

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(width, height);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var halfScreen = height * 0.5f;

            void LayoutBar(Image bar, float centerY)
            {
                bar.sprite = null;
                bar.color = Color.black;
                bar.raycastTarget = false;
                var r = bar.rectTransform;
                r.anchorMin = new Vector2(0.5f, 0.5f);
                r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2(0f, centerY);
                r.sizeDelta = new Vector2(width, offset);
            }

            var topImage = CreateUiImage("TopBar", canvasGo.transform, new Rect(0f, 0f, width, offset));
            LayoutBar(topImage, halfScreen + offset * 0.5f); // 屏幕外上方
            topBar = topImage.rectTransform;

            var bottomImage = CreateUiImage("BottomBar", canvasGo.transform, new Rect(0f, 0f, width, offset));
            LayoutBar(bottomImage, -(halfScreen + offset * 0.5f)); // 屏幕外下方
            bottomBar = bottomImage.rectTransform;
        }

        private static RectTransform CreateRect(
            string name, Transform parent, Vector2 anchor, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = UiLayer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private static Image CreateUiImage(string name, Transform parent, Rect rect)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = UiLayer;
            var rectTransform = (RectTransform)go.transform;
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0f, 0f);
            rectTransform.anchoredPosition = new Vector2(rect.x, rect.y);
            rectTransform.sizeDelta = new Vector2(rect.width, rect.height);

            var image = go.GetComponent<Image>();
            image.color = Color.white;
            return image;
        }

        private static Button CreateUiButton(
            string name, Transform parent, Vector2 anchor, Vector2 anchoredPosition,
            Vector2 sizeDelta, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.layer = UiLayer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;

            return go.GetComponent<Button>();
        }

        private static void SetStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
