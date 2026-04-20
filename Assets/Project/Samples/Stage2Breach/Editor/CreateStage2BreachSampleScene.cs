using Project.Core.Runtime.Framework;
using FrameworkAudioType = Project.Core.Runtime.Framework.AudioType;
using Project.Core.Runtime.Managers;
using Project.Narrative.Scripts;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;
using Project.Samples.Stage2Breach.Scripts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Samples.Stage2Breach.Editor
{
    public static class CreateStage2BreachSampleScene
    {
        private const string RootFolder = "Assets/Project/Samples/Stage2Breach";
        private const string ScenesFolder = RootFolder + "/Scenes";
        private const string ItemsFolder = RootFolder + "/Items";
        private const string ScenePath = ScenesFolder + "/Stage2_Breach_Sample.unity";
        private const string ChapterAssetPath = "Assets/Project/Narrative/Data/chapter_prologue_story.asset";

        [MenuItem("Tools/Project/Create Stage2 Breach Sample Scene")]
        public static void Create()
        {
            EnsureFolders();

            var toolKit = CreateToolItem("Tool_ToolKit", "tool_toolkit", "工具包", "用于拆解电子装置和维修配电箱。", ToolType.ToolKit, 5);
            var uvLight = CreateToolItem("Tool_UVLight", "tool_uvlight", "紫外线灯", "用于发现隐藏痕迹。", ToolType.UVLight, 99);
            var detector = CreateToolItem("Tool_Detector", "tool_detector", "便携探测器", "用于检测异常 EMF。", ToolType.Detector, 99);

            var watchClue = CreateClueItem("Clue_BloodyWatch", "clue_bloody_watch", "异常手表", "花坛中发现的男士手表。", true, true, "evidence_bloody_watch");
            var photoClue = CreateClueItem("Clue_LivingPhoto", "clue_living_photo", "团建合影", "客厅茶几中的团建照片。", false, false, "evidence_living_photo");
            var cameraClue = CreateClueItem("Clue_CameraRecord", "clue_camera_record", "安防录像", "拆下的室内监控记录。", false, false, "evidence_camera_record");
            var startupChapter = AssetDatabase.LoadAssetAtPath<VNChapterConfig>(ChapterAssetPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Stage2_Breach_Sample";

            var managersRoot = new GameObject("Managers");
            CreateManager<GameManager>(managersRoot.transform, "GameManager");
            CreateManager<SaveManager>(managersRoot.transform, "SaveManager");
            CreateManager<UIManager>(managersRoot.transform, "UIManager");
            CreateManager<AudioManager>(managersRoot.transform, "AudioManager");
            CreateManager<CGManager>(managersRoot.transform, "CGManager");
            CreateManager<FlagManager>(managersRoot.transform, "FlagManager");
            CreateManager<InteractionManager>(managersRoot.transform, "InteractionManager");
            CreateManager<InventoryManager>(managersRoot.transform, "InventoryManager");
            CreateManager<SanityManager>(managersRoot.transform, "SanityManager");
            CreateManager<EvidenceManager>(managersRoot.transform, "EvidenceManager");
            CreateManager<BranchManager>(managersRoot.transform, "BranchManager");
            CreateManager<GameLoopManager>(managersRoot.transform, "GameLoopManager");
            CreateManager<SceneFlowManager>(managersRoot.transform, "SceneFlowManager");
            var vnDirector = CreateManager<VNDirector>(managersRoot.transform, "VNDirector");
            AssignStartupChapter(vnDirector, startupChapter);

            CreateCamera();
            CreateLight();
            CreateEnvironment();

            var interactablesRoot = new GameObject("Interactables");
            CreateBreakerBox(interactablesRoot.transform);
            CreateFlowerbedWatch(interactablesRoot.transform, watchClue);
            CreateLivingPhoto(interactablesRoot.transform, photoClue);
            CreateLivingCamera(interactablesRoot.transform, cameraClue);
            CreateSofa(interactablesRoot.transform);

            var toolInput = CreateBootstrapper(toolKit, uvLight, detector);
            CreateUi(toolInput);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"Created sample scene at {ScenePath}");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Project", "Samples");
            EnsureFolder("Assets/Project/Samples", "Stage2Breach");
            EnsureFolder(RootFolder, "Scenes");
            EnsureFolder(RootFolder, "Items");
            EnsureFolder(RootFolder, "Scripts");
            EnsureFolder(RootFolder, "Editor");
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static T CreateManager<T>(Transform parent, string objectName) where T : ManagerBehaviour
        {
            var gameObject = new GameObject(objectName);
            gameObject.transform.SetParent(parent);
            var component = gameObject.AddComponent<T>();
            SetDontDestroyOnLoad(component, false);
            return component;
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            var lightComponent = lightObject.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 0.7f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateEnvironment()
        {
            var environmentRoot = new GameObject("Environment");
            CreateQuad(environmentRoot.transform, "BackyardFloor", new Vector3(-3.5f, -1.5f, 0f), new Vector3(6f, 3f, 1f), new Color(0.15f, 0.18f, 0.14f));
            CreateQuad(environmentRoot.transform, "LivingRoomFloor", new Vector3(3.5f, -1.5f, 0f), new Vector3(6f, 3f, 1f), new Color(0.2f, 0.16f, 0.14f));
            CreateQuad(environmentRoot.transform, "Divider", new Vector3(0f, -0.5f, 1f), new Vector3(0.25f, 5f, 1f), new Color(0.1f, 0.1f, 0.1f));
            CreateQuad(environmentRoot.transform, "BackyardLabel", new Vector3(-3.5f, 1.7f, 0f), new Vector3(2.5f, 0.5f, 1f), new Color(0.25f, 0.3f, 0.25f));
            CreateQuad(environmentRoot.transform, "LivingRoomLabel", new Vector3(3.5f, 1.7f, 0f), new Vector3(2.5f, 0.5f, 1f), new Color(0.35f, 0.25f, 0.22f));
        }

        private static Stage2BreachToolInput CreateBootstrapper(ToolItem toolKit, ToolItem uvLight, ToolItem detector)
        {
            var bootstrapperObject = new GameObject("Stage2BreachBootstrapper");
            var bootstrapper = bootstrapperObject.AddComponent<Stage2BreachBootstrapper>();
            var toolInput = bootstrapperObject.AddComponent<Stage2BreachToolInput>();
            var startupChapter = AssetDatabase.LoadAssetAtPath<VNChapterConfig>(ChapterAssetPath);
            var serializedObject = new SerializedObject(bootstrapper);
            serializedObject.FindProperty("openingChapter").objectReferenceValue = startupChapter;
            var toolsProperty = serializedObject.FindProperty("initialTools");
            toolsProperty.arraySize = 3;
            toolsProperty.GetArrayElementAtIndex(0).objectReferenceValue = toolKit;
            toolsProperty.GetArrayElementAtIndex(1).objectReferenceValue = uvLight;
            toolsProperty.GetArrayElementAtIndex(2).objectReferenceValue = detector;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            var toolInputObject = new SerializedObject(toolInput);
            var inputToolsProperty = toolInputObject.FindProperty("tools");
            inputToolsProperty.arraySize = 3;
            inputToolsProperty.GetArrayElementAtIndex(0).objectReferenceValue = toolKit;
            inputToolsProperty.GetArrayElementAtIndex(1).objectReferenceValue = uvLight;
            inputToolsProperty.GetArrayElementAtIndex(2).objectReferenceValue = detector;
            toolInputObject.FindProperty("selectedSlot").intValue = 0;
            toolInputObject.ApplyModifiedPropertiesWithoutUndo();
            return toolInput;
        }

        private static void CreateUi(Stage2BreachToolInput toolInput)
        {
            var uiRoot = new GameObject("UI");
            var canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(uiRoot.transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<GraphicRaycaster>();
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.transform.SetParent(uiRoot.transform, false);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();

            var hudPanel = CreatePanel("HUD", canvasObject.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(260f, 110f), new Color(0f, 0f, 0f, 0.45f));
            var sanityText = CreateText("SanityText", hudPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -10f), new Vector2(-10f, -35f), 20, TextAnchor.UpperLeft, "SAN 20/20");
            var evidenceText = CreateText("EvidenceText", hudPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -38f), new Vector2(-10f, -63f), 20, TextAnchor.UpperLeft, "证据 0/3");
            var containmentText = CreateText("ContainmentText", hudPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -66f), new Vector2(-10f, -91f), 20, TextAnchor.UpperLeft, "收容 0/3");

            var hintPanel = CreatePanel("HintPanel", canvasObject.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-280f, -20f), new Vector2(280f, -55f), new Color(0f, 0f, 0f, 0.35f));
            var hintText = CreateText("HintText", hintPanel.transform, Vector2.zero, Vector2.one, new Vector2(12f, 8f), new Vector2(-12f, -8f), 20, TextAnchor.MiddleLeft, string.Empty);

            var resultPanel = CreatePanel("ResultPanel", canvasObject.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-280f, -62f), new Vector2(280f, -102f), new Color(0f, 0f, 0f, 0.35f));
            var resultText = CreateText("ResultText", resultPanel.transform, Vector2.zero, Vector2.one, new Vector2(12f, 8f), new Vector2(-12f, -8f), 20, TextAnchor.MiddleLeft, string.Empty);
            var toolDragIndicator = CreateImage("ToolDragIndicator", canvasObject.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(96f, 96f), new Color(1f, 1f, 1f, 0.9f));

            var inspectorPanel = CreatePanel("InspectorPanel", canvasObject.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(320f, 180f), new Color(0f, 0f, 0f, 0.55f));
            var inspectorTitle = CreateText("InspectorTitle", inspectorPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -10f), new Vector2(-12f, -42f), 22, TextAnchor.UpperLeft, "调查");
            var inspectorBody = CreateText("InspectorBody", inspectorPanel.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 12f), new Vector2(-12f, -48f), 18, TextAnchor.UpperLeft, string.Empty);

            var toolbarPanel = CreatePanel("ToolbarPanel", canvasObject.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-360f, 190f), new Vector2(360f, 290f), new Color(0f, 0f, 0f, 0.45f));
            var toolButtons = new Button[3];
            var toolTexts = new Text[3];
            var toolHighlights = new Image[3];
            for (var i = 0; i < 3; i++)
            {
                var buttonObject = CreateButton($"ToolButton{i + 1}", toolbarPanel.transform, new Vector2(20f + i * 230f, 15f), new Vector2(210f, 85f), out var button, out var label, out var highlight);
                buttonObject.AddComponent<Stage2BreachToolDragHandler>().SlotIndex = i;
                label.alignment = TextAnchor.MiddleCenter;
                label.fontSize = 18;
                toolButtons[i] = button;
                toolTexts[i] = label;
                toolHighlights[i] = highlight;
                buttonObject.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 0.92f);
            }

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

            var choiceButtons = new Button[4];
            var choiceTexts = new Text[4];
            for (var i = 0; i < 4; i++)
            {
                var buttonObject = CreateButton($"ChoiceButton{i + 1}", choicesRoot.transform, new Vector2(0f, 0f), new Vector2(404f, 24f), out var button, out var label, out _);
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

            var viewObject = new GameObject("Stage2BreachSceneUiView");
            viewObject.transform.SetParent(canvasObject.transform, false);
            var view = viewObject.AddComponent<Stage2BreachSceneUiView>();
            var viewSerialized = new SerializedObject(view);
            viewSerialized.FindProperty("vnPanel").objectReferenceValue = vnPanel;
            viewSerialized.FindProperty("vnSpeakerText").objectReferenceValue = speakerText;
            viewSerialized.FindProperty("vnBodyText").objectReferenceValue = bodyText;
            viewSerialized.FindProperty("vnContinueHintText").objectReferenceValue = continueText;
            AssignObjectArray(viewSerialized.FindProperty("choiceButtons"), choiceButtons);
            AssignObjectArray(viewSerialized.FindProperty("choiceButtonTexts"), choiceTexts);
            viewSerialized.FindProperty("toolbarPanel").objectReferenceValue = toolbarPanel;
            AssignObjectArray(viewSerialized.FindProperty("toolButtons"), toolButtons);
            AssignObjectArray(viewSerialized.FindProperty("toolButtonTexts"), toolTexts);
            AssignObjectArray(viewSerialized.FindProperty("toolSelectionHighlights"), toolHighlights);
            viewSerialized.FindProperty("sanityText").objectReferenceValue = sanityText;
            viewSerialized.FindProperty("evidenceText").objectReferenceValue = evidenceText;
            viewSerialized.FindProperty("containmentText").objectReferenceValue = containmentText;
            viewSerialized.FindProperty("hintText").objectReferenceValue = hintText;
            viewSerialized.FindProperty("resultText").objectReferenceValue = resultText;
            viewSerialized.FindProperty("toolDragIndicator").objectReferenceValue = toolDragIndicator;
            viewSerialized.FindProperty("inspectorPanel").objectReferenceValue = inspectorPanel;
            viewSerialized.FindProperty("inspectorTitleText").objectReferenceValue = inspectorTitle;
            viewSerialized.FindProperty("inspectorBodyText").objectReferenceValue = inspectorBody;
            viewSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (toolInput != null)
            {
                toolInput.SelectSlot(0);
            }
        }

        private static void CreateBreakerBox(Transform parent)
        {
            var interactable = CreateInteractableBase(parent, "BreakerBox", new Vector3(-5f, -0.5f, 0f), new Vector3(1f, 1.6f, 0.2f));
            ConfigureInteractable(interactable, "backyard_breaker_box", null, "后院配电箱，雨夜中带着短路痕迹。", "配电箱发出异常嗡鸣。", true, true, ToolType.ToolKit, null, string.Empty, false, "breaker_fixed", "配电箱恢复供电，警报系统暂时失效。", "需要使用工具包才能处理配电箱。", 1, false, 0f, false, 0, false, false, false, FrameworkAudioType.None);
        }

        private static void CreateFlowerbedWatch(Transform parent, ClueItem clueItem)
        {
            var interactable = CreateInteractableBase(parent, "FlowerbedWatch", new Vector3(-2.5f, -1f, 0f), new Vector3(1.2f, 0.7f, 0.2f));
            ConfigureInteractable(interactable, "backyard_flowerbed_watch", clueItem, "泥土里露出半截男士手表。", "表带边缘残留诡异污渍。", true, true, ToolType.UVLight, clueItem, string.Empty, true, "watch_collected", "你收起了异常手表，并完成初步收容。", "缺少合适工具，手表上的痕迹难以辨认。", 1, true, 8f, true, 4, true, true, true, FrameworkAudioType.Hum);
        }

        private static void CreateLivingPhoto(Transform parent, ClueItem clueItem)
        {
            var interactable = CreateInteractableBase(parent, "LivingPhoto", new Vector3(2.2f, -0.6f, 0f), new Vector3(1.2f, 0.9f, 0.2f));
            ConfigureInteractable(interactable, "living_room_photo", clueItem, "茶几抽屉里压着一张团建合影。", "紫外线下，几名失踪者的面部被画上了叉号。", true, true, ToolType.None, clueItem, string.Empty, false, "photo_checked", "照片中的异常标记已经记录。", "", 0, false, 0f, false, 0, true, true, false, FrameworkAudioType.None);
        }

        private static void CreateLivingCamera(Transform parent, ClueItem clueItem)
        {
            var interactable = CreateInteractableBase(parent, "LivingCamera", new Vector3(5.2f, 0.8f, 0f), new Vector3(0.7f, 0.7f, 0.2f));
            ConfigureInteractable(interactable, "living_room_camera", clueItem, "天花板角落藏着一个安防摄像头。", "镜头内侧留下了模糊血指印。", true, true, ToolType.Detector, clueItem, string.Empty, false, "camera_checked", "你拆下了摄像头并提取到录像证据。", "探测器尚未锁定异常源。", 1, false, 0f, true, 5, false, false, true, FrameworkAudioType.Scream);
        }

        private static void CreateSofa(Transform parent)
        {
            var interactable = CreateInteractableBase(parent, "Sofa", new Vector3(4.2f, -1.5f, 0f), new Vector3(2.4f, 1f, 0.2f));
            ConfigureInteractable(interactable, "living_room_sofa", null, "沙发上残留着红酒酒渍和长发。", "空气里混杂着甜腻的血腥味。", true, false, ToolType.None, null, string.Empty, false, string.Empty, "沙发残留的异常气味让你更加不安。", "", 1, false, 0f, true, 2, false, false, true, FrameworkAudioType.Normal);
        }

        private static GameObject CreateInteractableBase(Transform parent, string objectName, Vector3 position, Vector3 scale)
        {
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = objectName;
            gameObject.transform.SetParent(parent);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().sharedMaterial = CreateColorMaterial(new Color(0.55f, 0.45f, 0.35f));
            gameObject.AddComponent<SimpleInteractable>();
            gameObject.AddComponent<SimpleInteractableAutoRegister>();
            gameObject.AddComponent<SimpleInteractableClickHandler>();
            gameObject.AddComponent<SampleInteractableRule>();
            return gameObject;
        }

        private static void ConfigureInteractable(
            GameObject gameObject,
            string interactableId,
            Item associatedItem,
            string description,
            string anomalyDescription,
            bool isActive,
            bool resolveOnClick,
            ToolType requiredToolType,
            ClueItem clueItem,
            string evidenceId,
            bool collectToContainment,
            string successFlag,
            string successText,
            string failureText,
            int failureSanityPenalty,
            bool registerTemperature,
            float temperatureValue,
            bool registerEmf,
            int emfValue,
            bool registerUv,
            bool uvValue,
            bool registerAudio,
            FrameworkAudioType audioType)
        {
            var interactable = gameObject.GetComponent<SimpleInteractable>();
            var interactableObject = new SerializedObject(interactable);
            interactableObject.FindProperty("interactableId").stringValue = interactableId;
            interactableObject.FindProperty("associatedItem").objectReferenceValue = associatedItem;
            interactableObject.FindProperty("isActive").boolValue = isActive;
            interactableObject.FindProperty("isCollected").boolValue = false;
            interactableObject.FindProperty("interactionState").stringValue = "default";
            interactableObject.FindProperty("description").stringValue = description;
            interactableObject.FindProperty("anomalyDescription").stringValue = anomalyDescription;
            interactableObject.ApplyModifiedPropertiesWithoutUndo();

            var rule = gameObject.GetComponent<SampleInteractableRule>();
            var ruleObject = new SerializedObject(rule);
            ruleObject.FindProperty("resolveOnClick").boolValue = resolveOnClick;
            ruleObject.FindProperty("requiredToolType").enumValueIndex = (int)requiredToolType;
            ruleObject.FindProperty("clueItem").objectReferenceValue = clueItem;
            ruleObject.FindProperty("evidenceId").stringValue = evidenceId;
            ruleObject.FindProperty("collectToContainment").boolValue = collectToContainment;
            ruleObject.FindProperty("deactivateOnSuccess").boolValue = true;
            ruleObject.FindProperty("markCollectedOnSuccess").boolValue = true;
            ruleObject.FindProperty("successFlag").stringValue = successFlag;
            ruleObject.FindProperty("successText").stringValue = successText;
            ruleObject.FindProperty("failureText").stringValue = failureText;
            ruleObject.FindProperty("failureSanityPenalty").intValue = failureSanityPenalty;
            ruleObject.FindProperty("registerTemperature").boolValue = registerTemperature;
            ruleObject.FindProperty("temperatureValue").floatValue = temperatureValue;
            ruleObject.FindProperty("registerEmf").boolValue = registerEmf;
            ruleObject.FindProperty("emfValue").intValue = emfValue;
            ruleObject.FindProperty("registerUv").boolValue = registerUv;
            ruleObject.FindProperty("uvValue").boolValue = uvValue;
            ruleObject.FindProperty("registerAudio").boolValue = registerAudio;
            ruleObject.FindProperty("audioType").enumValueIndex = (int)audioType;
            ruleObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ToolItem CreateToolItem(string assetName, string itemId, string displayName, string description, ToolType toolType, int durability)
        {
            var assetPath = $"{ItemsFolder}/{assetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ToolItem>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ToolItem>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            var serializedObject = new SerializedObject(asset);
            serializedObject.FindProperty("itemId").stringValue = itemId;
            serializedObject.FindProperty("displayName").stringValue = displayName;
            serializedObject.FindProperty("description").stringValue = description;
            serializedObject.FindProperty("maxDurability").intValue = durability;
            serializedObject.FindProperty("durability").intValue = durability;
            serializedObject.FindProperty("toolType").enumValueIndex = (int)toolType;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ClueItem CreateClueItem(string assetName, string itemId, string displayName, string description, bool isAnomaly, bool requiresContainment, string evidenceId)
        {
            var assetPath = $"{ItemsFolder}/{assetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ClueItem>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ClueItem>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            var serializedObject = new SerializedObject(asset);
            serializedObject.FindProperty("itemId").stringValue = itemId;
            serializedObject.FindProperty("displayName").stringValue = displayName;
            serializedObject.FindProperty("description").stringValue = description;
            serializedObject.FindProperty("isAnomaly").boolValue = isAnomaly;
            serializedObject.FindProperty("requiresContainment").boolValue = requiresContainment;
            serializedObject.FindProperty("evidenceId").stringValue = evidenceId;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void SetDontDestroyOnLoad(ManagerBehaviour manager, bool value)
        {
            var serializedObject = new SerializedObject(manager);
            serializedObject.FindProperty("dontDestroyOnLoad").boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignStartupChapter(VNDirector vnDirector, VNChapterConfig startupChapter)
        {
            if (vnDirector == null || startupChapter == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(vnDirector);
            serializedObject.FindProperty("startupChapter").objectReferenceValue = startupChapter;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateQuad(Transform parent, string objectName, Vector3 position, Vector3 scale, Color color)
        {
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            gameObject.name = objectName;
            gameObject.transform.SetParent(parent);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().sharedMaterial = CreateColorMaterial(color);
            return gameObject;
        }

        private static Material CreateColorMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            return material;
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

        private static Image CreateImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            var image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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

        private static GameObject CreateButton(string name, Transform parent, Vector2 position, Vector2 size, out Button button, out Text label, out Image highlight)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.2f, 0.28f, 0.92f);
            button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.28f, 0.3f, 0.42f, 0.98f);
            colors.pressedColor = new Color(0.12f, 0.14f, 0.2f, 1f);
            button.colors = colors;

            var highlightObject = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
            highlightObject.transform.SetParent(buttonObject.transform, false);
            var highlightRect = highlightObject.GetComponent<RectTransform>();
            highlightRect.anchorMin = Vector2.zero;
            highlightRect.anchorMax = Vector2.one;
            highlightRect.offsetMin = new Vector2(-3f, -3f);
            highlightRect.offsetMax = new Vector2(3f, 3f);
            highlight = highlightObject.GetComponent<Image>();
            highlight.color = new Color(1f, 0.8f, 0.25f, 0.8f);
            highlight.raycastTarget = false;
            highlight.enabled = false;
            highlightObject.transform.SetAsFirstSibling();

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
