using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Narrative.Scripts;
using Project.UI.Scripts;
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
            CreateUi();

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

        private static void CreateUi()
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
            var speakerText = CreateText("SpeakerText", vnPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -12f), new Vector2(-16f, -46f), 24, TextAnchor.UpperLeft, "旁白");
            var bodyText = CreateText("BodyText", vnPanel.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 70f), new Vector2(-16f, -54f), 22, TextAnchor.UpperLeft, string.Empty);
            var continueText = CreateText("ContinueHint", vnPanel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-220f, 12f), new Vector2(-16f, 38f), 18, TextAnchor.MiddleRight, "空格 / 右键继续");

            var choicesRoot = new GameObject("Choices");
            choicesRoot.transform.SetParent(vnPanel.transform, false);
            var choicesRect = choicesRoot.AddComponent<RectTransform>();
            choicesRect.anchorMin = new Vector2(1f, 0f);
            choicesRect.anchorMax = new Vector2(1f, 0f);
            choicesRect.pivot = new Vector2(1f, 0f);
            choicesRect.sizeDelta = new Vector2(404f, 140f);
            choicesRect.anchoredPosition = new Vector2(-16f, 50f);

            const int choiceCount = 4;
            var choiceButtons = new Button[choiceCount];
            var choiceTexts = new Text[choiceCount];
            for (var i = 0; i < choiceCount; i++)
            {
                var buttonObject = CreateButton($"ChoiceButton{i + 1}", choicesRoot.transform, out var button, out var label);
                var rect = buttonObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.offsetMin = new Vector2(0f, 84f - i * 28f);
                rect.offsetMax = new Vector2(0f, 112f - i * 28f);
                label.alignment = TextAnchor.MiddleCenter;
                buttonObject.GetComponent<Image>().color = new Color(0.22f, 0.24f, 0.32f, 0.95f);
                choiceButtons[i] = button;
                choiceTexts[i] = label;
            }

            var viewObject = new GameObject("VnSceneUiView");
            viewObject.transform.SetParent(canvasObject.transform, false);
            var view = viewObject.AddComponent<VnSceneUiView>();
            var vs = new SerializedObject(view);
            vs.FindProperty("vnPanel").objectReferenceValue = vnPanel;
            vs.FindProperty("vnSpeakerText").objectReferenceValue = speakerText;
            vs.FindProperty("vnBodyText").objectReferenceValue = bodyText;
            vs.FindProperty("vnContinueHintText").objectReferenceValue = continueText;
            AssignObjectArray(vs.FindProperty("choiceButtons"), choiceButtons);
            AssignObjectArray(vs.FindProperty("choiceButtonTexts"), choiceTexts);
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

        private static Text CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, int fontSize, TextAnchor alignment, string initialText)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;
            text.text = initialText;
            return text;
        }

        private static GameObject CreateButton(string name, Transform parent, out Button button, out Text label)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(404f, 24f);
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.2f, 0.28f, 0.92f);
            button = buttonObject.GetComponent<Button>();
            label = CreateText("Label", buttonObject.transform, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f), 18, TextAnchor.MiddleCenter, string.Empty);
            return buttonObject;
        }

        private static void AssignObjectArray(SerializedProperty property, Object[] objects)
        {
            property.arraySize = objects.Length;
            for (var i = 0; i < objects.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
            }
        }
    }
}
