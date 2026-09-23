using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Narrative.Scripts;
using Project.UI.Editor;
using Project.UI.Scripts;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Narrative.Editor
{
    /// <summary>
    /// 程序化创建 PX-2050「序幕：失踪者」纯 VN 场景。
    /// 菜单：Tools/Project/Story/PX-2050 创建序幕场景
    /// 依赖：先执行「PX-2050 导入章节」生成章节资产。
    /// </summary>
    public static class CreatePx2050PrologueScene
    {
        private const string ChapterPath = "Assets/Project/Narrative/Data/chapter_px2050.asset";
        private const string ScenePath = "Assets/Project/UI/Scenes/Px2050_Prologue.unity";

        [MenuItem("Tools/Project/Story/PX-2050 Create Prologue Scene")]
        public static void Create()
        {
            var startupChapter = AssetDatabase.LoadAssetAtPath<VNChapterConfig>(ChapterPath);
            if (startupChapter == null)
            {
                Debug.LogError($"[PX-2050] 未找到章节资产 {ChapterPath}，请先执行「PX-2050 导入章节」。");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Px2050_Prologue";

            var managersRoot = new GameObject("Managers");
            CreateManager<GameManager>(managersRoot.transform, "GameManager");
            CreateManager<UIManager>(managersRoot.transform, "UIManager");
            CreateManager<AudioManager>(managersRoot.transform, "AudioManager");
            CreateManager<CGManager>(managersRoot.transform, "CGManager");
            CreateManager<FlagManager>(managersRoot.transform, "FlagManager");
            CreateManager<SceneFlowManager>(managersRoot.transform, "SceneFlowManager");
            var vnDirector = CreateManager<VNDirector>(managersRoot.transform, "VNDirector");
            AssignStartupChapter(vnDirector, startupChapter);

            CreateCamera();
            CreateEventSystem();
            // 中文字体资产不存在时先生成，避免 VN 文本缺字。
            CreateUi(ApplyChineseFontToVnTexts.LoadOrCreateFontAsset());

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[PX-2050] 序幕场景已创建: {ScenePath}");
        }

        private static T CreateManager<T>(Transform parent, string objectName) where T : ManagerBehaviour
        {
            var gameObject = new GameObject(objectName);
            gameObject.transform.SetParent(parent);
            var component = gameObject.AddComponent<T>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("dontDestroyOnLoad").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private static void AssignStartupChapter(VNDirector vnDirector, VNChapterConfig startupChapter)
        {
            var serialized = new SerializedObject(vnDirector);
            serialized.FindProperty("startupChapter").objectReferenceValue = startupChapter;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.06f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void CreateEventSystem()
        {
            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static void CreateUi(TMP_FontAsset cjkFont)
        {
            var canvasObject = new GameObject("Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<GraphicRaycaster>();
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var vnPanel = CreatePanel("VNPanel", canvasObject.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 20f), new Vector2(-40f, 180f), new Color(0f, 0f, 0f, 0.72f));
            var speakerText = CreateText("SpeakerText", vnPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -12f), new Vector2(-16f, -46f), 24f, TextAlignmentOptions.TopLeft, "旁白", cjkFont);
            var bodyText = CreateText("BodyText", vnPanel.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 70f), new Vector2(-16f, -54f), 22f, TextAlignmentOptions.TopLeft, string.Empty, cjkFont);

            // 选项容器：全屏区域（挂在 Canvas 上，不再是 VNPanel 的子物体），
            // 这样选项可以居中竖排在画面中间；具体位置由 VnSceneUiView 的
            // choiceAlign / choiceVertical / choiceMargin 在运行时决定。
            var choicesRoot = new GameObject("Choices");
            choicesRoot.transform.SetParent(canvasObject.transform, false);
            var choicesRect = choicesRoot.AddComponent<RectTransform>();
            choicesRect.anchorMin = Vector2.zero;
            choicesRect.anchorMax = Vector2.one;
            choicesRect.offsetMin = Vector2.zero;
            choicesRect.offsetMax = Vector2.zero;
            choicesRect.pivot = new Vector2(0.5f, 0.5f);
            choicesRect.anchoredPosition = Vector2.zero;

            var viewObject = new GameObject("VnSceneUiView");
            viewObject.transform.SetParent(canvasObject.transform, false);
            var view = viewObject.AddComponent<VnSceneUiView>();
            var vs = new SerializedObject(view);
            vs.FindProperty("vnPanel").objectReferenceValue = vnPanel;
            vs.FindProperty("vnSpeakerText").objectReferenceValue = speakerText;
            vs.FindProperty("vnBodyText").objectReferenceValue = bodyText;
            vs.FindProperty("choiceRoot").objectReferenceValue = choicesRect;

            // 选项按钮是生成式的：有预制体就用预制体，没有则运行时用代码生成同样式按钮。
            var choiceButtonPrefab = AssetDatabase.LoadAssetAtPath<Button>(SetupVnChoiceButtons.PrefabPath);
            if (choiceButtonPrefab != null)
            {
                vs.FindProperty("choiceButtonPrefab").objectReferenceValue = choiceButtonPrefab;
            }

            // 兜底按钮也用同一张 VN 底板图，保证两条生成路径外观一致。
            var choicePlate = AssetDatabase.LoadAssetAtPath<Sprite>(SetupVnChoiceButtons.ChoicePlatePath);
            if (choicePlate != null)
            {
                vs.FindProperty("choiceButtonSprite").objectReferenceValue = choicePlate;
            }

            vs.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, float fontSize, TextAlignmentOptions alignment, string initialText, TMP_FontAsset font)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var text = textObject.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }

            text.fontSize = fontSize;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.color = Color.white;
            text.text = initialText;
            return text;
        }
    }
}
