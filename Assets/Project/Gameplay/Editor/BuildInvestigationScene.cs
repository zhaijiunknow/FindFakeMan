using System;
using System.Collections.Generic;
using System.IO;

// UnityEngine 也有一个 Object：加了 `using System;`（房间扫描要用 StringComparer/StringComparison）之后，
// 文件里原有的 Object.DestroyImmediate / Object.FindObjectsByType 会变成二义引用（CS0104），所以钉一下。
using Object = UnityEngine.Object;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts;
using Project.Gameplay.Scripts.Case;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;
using Project.Gameplay.Scripts.Tools;
using Project.Narrative.Scripts;
using Project.UI.BigApp;
using Project.UI.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Gameplay.Editor
{
    /// <summary>
    /// 一键搭「专门负责玩法的探索场景」：PX-2050 别墅客厅。
    ///
    /// 结构（玩法全部在 UI 里，项目自有、不依赖 Sample）：
    ///   Canvas(1920×1080)
    ///     └─ BigSoftware  「Electronic Control System」窗口
    ///          ├─ MainPanel → RoomView(16:9 贴合) → 7 张别墅客厅图层（3b = 夜版）
    ///          │                                  每件家具一张整帧图，图自己就是按钮
    ///          │                                  （alphaHitTest 只在不透明像素上吃射线）
    ///          ├─ SidePanel   Health（四颗心 = SAN）/ Collect（三格 = 收容）
    ///          ├─ BottomLeft  证据 / 收容 读数
    ///          ├─ Items 四格 = 工具槽（拖拽入口）+ 右下雷达
    ///          └─ 检视面板 / 提示 / 结果 / 拖拽影子
    ///     └─ Managers 13 个 manager（场景级）
    ///     └─ InvestigationBootstrapper（InvestigationSceneBootstrapper + ToolBeltInput）
    ///
    /// 菜单：Tools/Project/Gameplay/Build Investigation Scene
    /// 前置：先跑 Tools/Project/Gameplay/Fix Living Room Sprite Import（别墅客厅的图默认导成了 Multiple 且没有 Sprite 子资源）。
    /// 注意：本工具会新建并**覆盖**目标场景文件（属于一键重建型工具，和 PanelValidationBuilder 同类）。
    /// </summary>
    public static class BuildInvestigationScene
    {
        /// <summary>场景路径：和项目里其它场景放在一起（UI/Scenes），不要在 Gameplay 下另开一套。</summary>
        private const string ScenePath = "Assets/Project/UI/Scenes/Px2050_Villa.unity";
        private const string ItemFolder = "Assets/Project/Gameplay/ScriptableObjects/Items";
        private const string ToolFolder = "Assets/Project/Gameplay/ScriptableObjects/Tools";
        private const string BigAppFolder = "Assets/Project/Resource/UI/大软件";
        private const string RoomFolder = "Assets/Project/Resource/别墅客厅";

        /// <summary>房间图层的夜版前缀：文件夹里符合它的图层才进关卡（`3a` 是白天版）。</summary>
        private const string NightPrefix = "3b";

        /// <summary>文件名里带它的当底图，不当家具。</summary>
        private const string BackgroundKeyword = "背景";

        /// <summary>文件名里带它的当特写图层。</summary>
        private const string CloseUpKeyword = "特写";

        /// <summary>
        /// 特写 → 家具 的对照表（只有名字对不上时才需要）。
        /// 现有美术：特写叫「3b抽屉特写」，家具叫「3b木桌」，按「X特写 归 X」的约定匹配不上，所以显式指一下。
        /// 以后出美术按「`<家具名>特写.png`」命名就不用写这张表。
        /// </summary>
        private static readonly Dictionary<string, string> CloseUpOwnerOverrides = new()
        {
            { "抽屉特写", "木桌" },
        };

        /// <summary>窗口预制体：序幕开机后露出来的那个终端窗口就是它（里面含配置好的 BigApp 实例）。</summary>
        private const string MainPrefabPath = "Assets/Project/UI/Prefabs/Main.prefab";

        /// <summary>小软件窗口（玩法里当「笔记」用）。自带 UIWindowManager，实例化即可用。</summary>
        private const string SmallAppPrefabPath = "Assets/Project/UI/Prefabs/SmallApp.prefab";

        /// <summary>代码生成的手势表盘贴图放这里（圆环 + 箭头）。</summary>
        private const string GestureArtFolder = "Assets/Project/Resource/UI/Gesture";

        /// <summary>手势表盘贴图是否每次重建都重新生成（改了形状不用手动删图）。</summary>
        private const bool ForceRebuildGestureArt = true;

        /// <summary>序章章节（运行时从 Resources 解析的就是这一份）。</summary>
        private const string PrologueChapterPath = "Assets/Resources/Narrative/chapter_prologue_story.asset";

        /// <summary>序章章节的源文件副本，一并改，免得两边不一致。</summary>
        private const string PrologueChapterSourcePath = "Assets/Project/Narrative/Data/chapter_prologue_story.asset";

        private static string SceneName => Path.GetFileNameWithoutExtension(ScenePath);

        private const float RefW = 1920f;
        private const float RefH = 1080f;

        [MenuItem("Tools/Project/Gameplay/Build Investigation Scene")]
        public static void Build()
        {
            // 这个工具会 NewScene(Single)，当前打开的场景会被替换掉 —— 先把没保存的改动处理掉，别把别人的场景丢了。
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Villa] 已取消构建（当前场景未保存）。");
                return;
            }

            // 别墅客厅的图默认导成了 Multiple 且 spriteSheet 是空的（等于没有 Sprite 子资源），
            // 这里先自动修一遍导入设置，省得美术/程序手动跑第二步。
            FixLivingRoomSpriteImport.Run();

            EnsureFolders();

            // 物品图标（工具 + 线索）都由 GenerateToolIcons 用代码画：描边 + 同色柔光，
            // 工具走浅青白、线索走各自主题色。想重画用菜单 Tools/Project/Gameplay/Generate Item Icons。
            // 之前探测器借用的是 leida.png 那张雷达盘 —— 那是"大软件"里雷达面板本身的图，
            // 拿来当工具图标会和面板撞脸，所以在建造工具里换成独立图标。
            var toolKit = CreateToolAsset("Tool_ToolKit", "tool_toolkit", "工具包", "拆开夹层、撬开柜门用的便携工具包。", ToolType.ToolKit, 5, GenerateToolIcons.EnsureIcon(ToolType.ToolKit));
            var uvLight = CreateToolAsset("Tool_UVLight", "tool_uvlight", "紫外线灯", "照出体液、荧光痕迹的手持紫外线灯。", ToolType.UVLight, 99, GenerateToolIcons.EnsureIcon(ToolType.UVLight));
            var detector = CreateToolAsset("Tool_Detector", "tool_detector", "便携探测器", "扫描电磁异常（EMF）的便携探测器。", ToolType.Detector, 99, GenerateToolIcons.EnsureIcon(ToolType.Detector));
            var thermometer = CreateToolAsset("Tool_Thermometer", "tool_thermometer", "红外温度计", "读取表面温度的手持红外温度计。", ToolType.Thermometer, 99, GenerateToolIcons.EnsureIcon(ToolType.Thermometer));

            // 录音设备是第 4 种读数（温度 / EMF / 紫外 / 录音）。工具槽只有 4 格，所以它顶掉工具包：
            // 随机案情里"工具包"没有读数作用（它原本是"撬沙发夹层"那段固定内容用的），
            // 资产仍然照建、随时能装回去（给第 5 格留位）。
            var recorder = CreateToolAsset("Tool_Recorder", "tool_recorder", "录音设备", "录下这件东西周围之前发生过的声音。", ToolType.Recorder, 99, GenerateToolIcons.EnsureIcon(ToolType.Recorder));

            var tools = new[] { recorder, uvLight, detector, thermometer };

            Debug.Log($"[Villa] 装备的 4 件工具图标：录音设备={(recorder != null && recorder.Icon != null)}，"
                      + $"紫外线灯={(uvLight != null && uvLight.Icon != null)}，探测器={(detector != null && detector.Icon != null)}，"
                      + $"温度计={(thermometer != null && thermometer.Icon != null)}"
                      + $"（工具包已生成但没装备，图标={(toolKit != null && toolKit.Icon != null)}；缺图标就只显示空槽）");

            // 线索图标同样代码生成（收容格是"没有图标就不显示"，所以没图标的话收了线索格子里是空的）。
            var clueStain = CreateClueAsset("Clue_WaterStain", "clue_water_stain", "荧光水渍", "地毯边上一小片干掉的痕迹，在紫外线下发着不该有的冷光。", true, true, "evidence_water_stain", GenerateToolIcons.EnsureClueIcon("Clue_WaterStain"));
            var clueSofaHair = CreateClueAsset("Clue_SofaHair", "clue_sofa_hair", "坐垫夹层的头发", "缝在坐垫夹层里的头发，颜色和长度都不属于这栋房子的人。", true, true, "evidence_sofa_hair", GenerateToolIcons.EnsureClueIcon("Clue_SofaHair"));
            var clueDrawerRecord = CreateClueAsset("Clue_DrawerRecord", "clue_drawer_record", "抽屉里的记录", "木桌抽屉夹层里的一张手写记录，字迹在几处突然变形。", true, true, "evidence_drawer_record", GenerateToolIcons.EnsureClueIcon("Clue_DrawerRecord"));

            Debug.Log($"[Villa] 线索图标：水渍={(clueStain != null && clueStain.Icon != null)}，头发={(clueSofaHair != null && clueSofaHair.Icon != null)}，"
                      + $"记录={(clueDrawerRecord != null && clueDrawerRecord.Icon != null)}（收容格是「没有图标就不显示」，所以这三个必须有）");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var leftover in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(leftover);
            }

            CreateManagers();
            CreateCamera();
            CreateEventSystem();
            // 先建 bootstrapper：ToolBeltInput 挂在它身上，UI 里要接线到它的工具槽。
            // 装备位只有 4 格，但工具池有 5 件：把 5 件都给案情，让它每局随机少带一件。
            CreateBootstrapper(tools, new[] { toolKit, uvLight, detector, thermometer, recorder },
                new Item[] { clueStain, clueSofaHair, clueDrawerRecord });

            var canvas = CreateCanvas();
            var roomView = CreateMainWindow(canvas, tools);
            BuildRooms(roomView);
            CreateCaseResultPanel(canvas);
            CreateSmallApp(canvas);

            EditorSceneManager.MarkSceneDirty(scene);
            // 检查返回值：保存失败（目录不存在/只读等）就别接着往下打印"已生成"，免得日志撒谎。
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"[Villa] 保存场景失败：{ScenePath}（检查目录是否存在、是否只读）");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AddToBuildSettings();
            WirePrologueEndAction();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[Villa] 玩法场景已生成：{ScenePath}（工具 {ToolFolder}，道具 {ItemFolder}）");
        }

        // ---------- 资产 ----------

        private static void EnsureFolders()
        {
            // 场景和项目里其它场景放一起（UI/Scenes）；这里只保证目录存在，文件不存在就报错。
            EnsureFolder("Assets/Project/UI", "Scenes");
            EnsureFolder("Assets/Project/Resource/UI", "Gesture");
            EnsureFolder("Assets/Project/Gameplay", "ScriptableObjects");
            EnsureFolder("Assets/Project/Gameplay/ScriptableObjects", "Items");
            EnsureFolder("Assets/Project/Gameplay/ScriptableObjects", "Tools");
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static ToolItem CreateToolAsset(string assetName, string itemId, string displayName, string description, ToolType toolType, int durability, Sprite icon)
        {
            var path = $"{ToolFolder}/{assetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ToolItem>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ToolItem>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var so = new SerializedObject(asset);
            so.FindProperty("itemId").stringValue = itemId;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("toolType").enumValueIndex = (int)toolType;
            so.FindProperty("maxDurability").intValue = durability;
            so.FindProperty("durability").intValue = durability;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ClueItem CreateClueAsset(string assetName, string itemId, string displayName, string description, bool isAnomaly, bool requiresContainment, string evidenceId, Sprite icon)
        {
            var path = $"{ItemFolder}/{assetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ClueItem>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ClueItem>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var so = new SerializedObject(asset);
            so.FindProperty("itemId").stringValue = itemId;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("isAnomaly").boolValue = isAnomaly;
            so.FindProperty("requiresContainment").boolValue = requiresContainment;
            so.FindProperty("evidenceId").stringValue = evidenceId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // ---------- 场景骨架 ----------

        private static void CreateManagers()
        {
            var root = new GameObject("Managers");
            CreateManager<GameManager>(root.transform, "GameManager");
            CreateManager<SaveManager>(root.transform, "SaveManager");
            CreateManager<UIManager>(root.transform, "UIManager");
            CreateManager<AudioManager>(root.transform, "AudioManager");
            CreateManager<CGManager>(root.transform, "CGManager");
            CreateManager<FlagManager>(root.transform, "FlagManager");
            CreateManager<InteractionManager>(root.transform, "InteractionManager");
            CreateManager<InventoryManager>(root.transform, "InventoryManager");

            // 装备容量必须和 UI 的槽位数一致：预制体里是 4 个槽（itemButton1..4），
            // 而 InventoryManager 的默认容量是 3 —— 不改的话关卡最多只带 3 件工具，
            // 会和序幕 UI 上显示的 4 件对不上（交接单传 4 件也会被这里截成 3 件）。
            var inventoryGo = root.transform.Find("InventoryManager");
            var inventory = inventoryGo != null ? inventoryGo.GetComponent<InventoryManager>() : null;
            if (inventory != null)
            {
                var iso = new SerializedObject(inventory);
                iso.FindProperty("equipmentCapacity").intValue = 4;
                iso.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[Villa] 找不到 InventoryManager，装备容量没设成 4（关卡可能只带 3 件工具）。");
            }
            CreateManager<SanityManager>(root.transform, "SanityManager");
            CreateManager<EvidenceManager>(root.transform, "EvidenceManager");
            CreateManager<BranchManager>(root.transform, "BranchManager");
            CreateManager<GameLoopManager>(root.transform, "GameLoopManager");
            CreateManager<SceneFlowManager>(root.transform, "SceneFlowManager");
        }

        private static T CreateManager<T>(Transform parent, string objectName) where T : ManagerBehaviour
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var component = go.AddComponent<T>();
            // 场景级 manager：离开这个场景就销毁，避免把上一局的 SAN / 证据带回来。
            var so = new SerializedObject(component);
            so.FindProperty("dontDestroyOnLoad").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.06f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            go.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateBootstrapper(ToolItem[] tools, ToolItem[] loadoutPool, Item[] items)
        {
            var go = new GameObject("InvestigationBootstrapper");
            var bootstrapper = go.AddComponent<InvestigationSceneBootstrapper>();
            go.AddComponent<ToolBeltInput>();

            // 通用关卡器：本局案情（身份 + 读数 + 带哪几件）挂在组装点同一根节点上 —— 组装点自己在 Awake 里
            // GetComponent 找它，所以不用在场景里连引用。种子开关 / 读数区间 / 异常件数 / 装备池都在它身上调。
            var caseDirector = go.AddComponent<CaseDirector>();
            var caseSo = new SerializedObject(caseDirector);
            AssignObjectArray(caseSo.FindProperty("loadoutPool"), loadoutPool);
            caseSo.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(bootstrapper);
            AssignObjectArray(so.FindProperty("initialTools"), tools);
            AssignObjectArray(so.FindProperty("knownItems"), items);
            so.FindProperty("initialSanity").intValue = 4;
            so.FindProperty("evidenceGoal").intValue = 3;
            so.FindProperty("branchSeed").intValue = 2050;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- 大软件窗口（实例化 Main 预制体）----------

        /// <summary>
        /// 实例化 `Main.prefab` 当窗口，并把房间视口 / HUD 接上去。
        ///
        /// 为什么用 Main.prefab 而不是自己拼一套：它就是序幕里开机之后露出来的那个终端窗口
        /// （`BuildTerminalBootTransition` 也是实例化它、命名成 MainUI），而且里面的 BigApp 实例已经被配置好了
        /// （补了 `red`、把 `nothink/Status` 换成 TMP、给 `itemButton4` 补了 ToolSlotButton、每个槽位的 slotIndex/detail 都覆盖过）。
        /// 自己再拼一份只会慢慢和它不一致 —— 改预制体不会影响场景里的副本就是问题所在。
        /// </summary>
        private static RectTransform CreateMainWindow(Canvas canvas, ToolItem[] tools)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[Villa] 找不到 {MainPrefabPath}，窗口建不出来。");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            if (instance == null)
            {
                Debug.LogError($"[Villa] 实例化 {MainPrefabPath} 失败。");
                return null;
            }

            instance.name = "Main";
            // 和序幕第 7 拍「还原全屏」保持一致：整块窗口铺满画布。
            Stretch((RectTransform)instance.transform);

            var root = instance.transform;

            // ---- 预制体里已有的节点（路径照 BigApp.prefab 的实际层级，见 Assets/Project/Gameplay/README.md）----
            var gameViewport = root.Find("BigApp/game") as RectTransform;
            var sanitySlider = root.Find("BigApp/box/life")?.GetComponent<Slider>();
            var containmentLabel = root.Find("BigApp/box/Collect")?.GetComponent<TextMeshProUGUI>();
            var detailPanel = root.Find("BigApp/nothink")?.GetComponent<ItemDetailPanel>();
            var detailName = root.Find("BigApp/nothink/Name")?.GetComponent<TextMeshProUGUI>();
            var detailDesc = root.Find("BigApp/nothink/Desc")?.GetComponent<TextMeshProUGUI>();
            var detailStatus = root.Find("BigApp/nothink/Status")?.GetComponent<TextMeshProUGUI>();

            var slotFrames = new Image[4];
            var slotIcons = new Image[4];
            var slotRects = new RectTransform[4];
            for (var i = 0; i < slotFrames.Length; i++)
            {
                var slot = root.Find($"BigApp/item/boxcontent/itemButton{i + 1}");
                slotFrames[i] = slot != null ? slot.GetComponent<Image>() : null;
                slotIcons[i] = slot != null ? slot.Find("item")?.GetComponent<Image>() : null;
                slotRects[i] = slot as RectTransform;

                // 图标是方形的，槽位不一定是方的：保持比例，别把图标拉扁。
                if (slotIcons[i] != null)
                {
                    slotIcons[i].preserveAspect = true;
                }
            }

            var containmentIcons = new Image[3];
            for (var i = 0; i < containmentIcons.Length; i++)
            {
                containmentIcons[i] = root.Find($"BigApp/box/boxcontent/boxButton{i + 1}/box")?.GetComponent<Image>();

                // 线索图标同样是方形的：保持比例，别拉扁。
                if (containmentIcons[i] != null)
                {
                    containmentIcons[i].preserveAspect = true;
                }
            }

            // ---- 房间视口：直接铺满预制体里的 game 面板（房间图层是同尺寸整帧图，套在一起对齐）----
            var roomGo = new GameObject("RoomView", typeof(RectTransform));
            var roomView = (RectTransform)roomGo.transform;
            if (gameViewport != null)
            {
                roomView.SetParent(gameViewport, false);
            }
            else
            {
                Debug.LogWarning("[Villa] 预制体里找不到 BigApp/game 视口，房间改挂在窗口根上。");
                roomView.SetParent(root, false);
            }

            Stretch(roomView);

            // game 面板不是 16:9（约 1.98:1），直接铺满会把房间横向拉宽约 11%；
            // 用 AspectRatioFitter 在面板里按 16:9 居中贴合，房间图层都以 RoomView 为基准，所以始终对齐。
            var fitter = roomGo.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;

            // ---- 预制体里没有的元素：只剩拖拽影子 ----
            // 提示 / 结果 / 证据**不再新建文本**：直接复用 nothink 面板里已有的 Status / Desc / Name 三行，
            // InvestigationHudView 会占用前先备份原文、计时结束后还原，所以详情区写的内容不会被吃掉。
            var ghost = Img(root, "ToolDragGhost", null, 0f, 0f, 0f, 0f);
            var ghostRect = ghost.rectTransform;
            ghostRect.anchorMin = ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRect.pivot = new Vector2(0.5f, 0.5f);
            ghostRect.anchoredPosition = Vector2.zero;
            ghostRect.sizeDelta = new Vector2(96f, 96f);
            ghost.gameObject.SetActive(false);

            // ---- HUD 视图：挂在预制体实例上（给实例加组件 = 场景覆盖，不会改到预制体）----
            var hud = instance.AddComponent<InvestigationHudView>();
            var hso = new SerializedObject(hud);
            hso.FindProperty("sanitySlider").objectReferenceValue = sanitySlider;
            hso.FindProperty("containmentLabel").objectReferenceValue = containmentLabel;
            hso.FindProperty("detailPanel").objectReferenceValue = detailPanel;
            hso.FindProperty("detailNameText").objectReferenceValue = detailName;
            hso.FindProperty("detailDescText").objectReferenceValue = detailDesc;
            hso.FindProperty("detailStatusText").objectReferenceValue = detailStatus;
            AssignObjectArray(hso.FindProperty("toolSlotFrames"), slotFrames);
            AssignObjectArray(hso.FindProperty("toolSlotIcons"), slotIcons);
            AssignObjectArray(hso.FindProperty("containmentIcons"), containmentIcons);
            hso.FindProperty("itemNormalSprite").objectReferenceValue = LoadSprite($"{BigAppFolder}/item_noselect.png");
            hso.FindProperty("itemSelectedSprite").objectReferenceValue = LoadSprite($"{BigAppFolder}/item_select.png");
            hso.FindProperty("toolDragGhost").objectReferenceValue = ghost;
            hso.ApplyModifiedPropertiesWithoutUndo();

            // ---- 雷达（探测器）：扫描扇形与动效都挂到 MeterBox 下面（和手势圆环共用同一个圆）----
            // 这里只确认雷达存在；实际挂载放在下面的表盘块里做，顺序是 Sweep → DirectionMeter（环压在扇形上）。
            var radarPanel = root.Find("BigApp/leida") as RectTransform;
            if (radarPanel == null)
            {
                Debug.LogWarning("[Villa] 预制体里找不到 BigApp/leida（雷达），雷达动效没接上。");
            }

            // ---- 方向拖拽（设计表 §4.2）：挂在 nothink 面板上 ----
            var nothinkPanel = root.Find("BigApp/nothink");
            if (nothinkPanel == null)
            {
                Debug.LogWarning("[Villa] 预制体里找不到 BigApp/nothink，精查面板的方向拖拽没接上。");
            }
            else
            {
                // 面板自己必须吃射线，否则收不到拖拽事件。
                var nothinkImage = nothinkPanel.GetComponent<Image>();

                // 结论选项**不再**挂在详情区：它已经搬到小软件窗口的「笔记」页（见 CreateSmallApp）。
                // nothink 回归纯详情区 —— 看单件东西 + 方向手势提示，不再显示证据/结论。
                Debug.Log("[Villa] nothink 不再挂结论选项（已移到 SmallApp 的笔记页）。");                if (nothinkImage != null)
                {
                    nothinkImage.raycastTarget = true;
                }

                // 底特律式表盘：圆环（Filled/Radial360，从正上方顺时针）+ 四个方向箭头（当前方向的会亮）+ 环内动作名。
                var ringSprite = EnsureRingSprite();
                var arrowSprite = EnsureArrowSprite();

                // 表盘挂在雷达（leida）**下面** —— 跟着雷达走，不用抄矩形。
                // 中间垫一层 MeterBox 做内缩：leida 上有 Mask，超出圆盘的部分会被裁掉，
                // 所以圆环要落在圆盘内缘里面，箭头也不能贴到矩形边上。
                var meterHost = radarPanel != null ? (Transform)radarPanel : nothinkPanel;

                var boxGo = new GameObject("MeterBox", typeof(RectTransform));
                boxGo.transform.SetParent(meterHost, false);
                var boxRect = (RectTransform)boxGo.transform;
                // 铺满雷达矩形，不内缩：leida 上的 Mask 本来就会把超出圆盘的东西裁掉，
                // 所以让圆环直接落在圆盘边缘上。
                Stretch(boxRect);

                // MeterBox 自己锁成正圆：底环（拉伸铺满）和 DirectionMeter（FitInParent 取短边）
                // 因此拿到同一个方形 —— 两个环一样大、也都是正圆，底环不会被拉成椭圆。
                var boxFitter = boxGo.AddComponent<AspectRatioFitter>();
                boxFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                boxFitter.aspectRatio = 1f;

                // 扫描扇形也放进 MeterBox：它和手势圆环共用同一个正方形空间（都用短边定尺寸），
                // 所以两者同心、同尺寸 —— 环就"长"在雷达盘上，而不是两个各转各的圆。
                var sweepSprite = EnsureRadarSweepSprite();
                var sweep = Img(boxRect, "Sweep", sweepSprite, 0f, 0f, 1f, 1f);
                sweep.preserveAspect = true; // 保持正圆
                sweep.color = new Color(0.55f, 0.85f, 1f, 0.30f);

                var radarEffects = boxGo.AddComponent<RadarEffects>();
                var rso = new SerializedObject(radarEffects);
                rso.FindProperty("sweep").objectReferenceValue = sweep;
                rso.ApplyModifiedPropertiesWithoutUndo();

                var hsoRadar = new SerializedObject(hud);
                hsoRadar.FindProperty("radar").objectReferenceValue = radarEffects;
                hsoRadar.ApplyModifiedPropertiesWithoutUndo();

                Debug.Log("[Villa] 雷达动效已接（挂在 MeterBox 下、和手势环同一个圆）：常驻扫描、拖工具加速、判定有效时闪一下");

                var meterGo = new GameObject("DirectionMeter", typeof(RectTransform));
                meterGo.transform.SetParent(boxGo.transform, false);
                var meterRect = (RectTransform)meterGo.transform;
                Stretch(meterRect);
                var meterFitter = meterGo.AddComponent<AspectRatioFitter>();
                meterFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                meterFitter.aspectRatio = 1f; // 正圆

                // 底环（RingBack）放在 MeterBox 下 —— 它和扫描扇形一样**常驻**，
                // 不跟着 DirectionMeter 一起开关（没选中物品时也要看得见这个表盘）。
                var ringBack = Img(boxRect, "RingBack", ringSprite, 0f, 0f, 1f, 1f);
                ringBack.color = new Color(1f, 1f, 1f, 0.10f);
                // 底环只是显示，不吃射线（方向拖拽的接收面在 game 板块上，见下面）。

                var ringFill = Img(meterRect, "RingFill", ringSprite, 0f, 0f, 1f, 1f);
                ringFill.color = new Color(0.42f, 0.66f, 1f, 0.30f);

                var arrows = new[]
                {
                    CreateArrow(meterRect, "ArrowUp", arrowSprite, new Vector2(0.5f, 0.93f), 0f),
                    CreateArrow(meterRect, "ArrowDown", arrowSprite, new Vector2(0.5f, 0.07f), 180f),
                    CreateArrow(meterRect, "ArrowLeft", arrowSprite, new Vector2(0.07f, 0.5f), 90f),
                    CreateArrow(meterRect, "ArrowRight", arrowSprite, new Vector2(0.93f, 0.5f), -90f),
                };

                var widgetFont = ApplyChineseFontToVnTexts.LoadOrCreateFontAsset();
                var actionLabel = Txt(meterRect, "ActionText", string.Empty, 0.12f, 0.40f, 0.76f, 0.20f, 22, TextAnchor.MiddleCenter, widgetFont);

                var progressView = meterGo.AddComponent<DirectionProgressView>();
                var pso = new SerializedObject(progressView);
                pso.FindProperty("root").objectReferenceValue = meterGo;
                pso.FindProperty("ringFill").objectReferenceValue = ringFill;
                AssignObjectArray(pso.FindProperty("directionArrows"), arrows);
                pso.FindProperty("actionText").objectReferenceValue = actionLabel;
                // 进度弧跟着雷达扫描角转：让"进度"和"扫描"看起来是同一件事。
                if (radarEffects != null)
                {
                    pso.FindProperty("radar").objectReferenceValue = radarEffects;
                }

                pso.ApplyModifiedPropertiesWithoutUndo();

                // 方向拖拽挂在 **game 板块**上：玩家直接对着房间做手势，雷达表盘只负责显示。
                // game 的 Image 要吃射线才收得到拖拽；从家具命中区上按下去的拖拽会沿层级冒泡到这里
                // （uGUI 的拖拽会向上找第一个 IBeginDragHandler），命中区自己没有拖拽处理器，所以不会抢。
                var dragHost = gameViewport != null ? gameViewport.gameObject : boxGo;
                if (gameViewport != null)
                {
                    var gameImage = gameViewport.GetComponent<Image>();
                    if (gameImage != null)
                    {
                        gameImage.raycastTarget = true;
                    }
                }

                var dragHandler = dragHost.AddComponent<InspectorDragHandler>();
                var dso = new SerializedObject(dragHandler);
                dso.FindProperty("hud").objectReferenceValue = hud;
                dso.FindProperty("progressView").objectReferenceValue = progressView;
                dso.FindProperty("upAction").enumValueIndex = (int)ItemActionKind.Pickup;
                dso.FindProperty("downAction").enumValueIndex = (int)ItemActionKind.Discard;
                dso.FindProperty("leftAction").enumValueIndex = (int)ItemActionKind.Equip;
                dso.FindProperty("rightAction").enumValueIndex = (int)ItemActionKind.Inspect;
                dso.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Villa] 方向拖拽已接：上=拾取 下=丢弃 左=装备 右=检视；带进度指示器（没填满松手会缓慢回退）");
            }

            // ---- 工具输入接线：槽位 rect 指向预制体里的四个格子 ----
            var input = Object.FindFirstObjectByType<ToolBeltInput>();
            if (input == null)
            {
                Debug.LogWarning("[Villa] 没找到 ToolBeltInput，工具槽没接上。");
            }
            else
            {
                var iso = new SerializedObject(input);
                AssignObjectArray(iso.FindProperty("slotRects"), slotRects);
                AssignObjectArray(iso.FindProperty("tools"), tools);
                iso.ApplyModifiedPropertiesWithoutUndo();
            }

            Debug.Log($"[Villa] 已实例化 {MainPrefabPath}：视口 BigApp/game={(gameViewport != null)}，SAN Slider={(sanitySlider != null)}，详情区={(detailPanel != null)}，工具槽1={(slotRects[0] != null)}，收容格1={(containmentIcons[0] != null)}，方向拖拽={(nothinkPanel != null)}。");
            return roomView;
        }

        // ---------- 房间图层 + 交互物 ----------

        private static void BuildRooms(RectTransform roomView)
        {
            if (roomView == null)
            {
                Debug.LogError("[Villa] 找不到 RoomView，房间图层没建出来。");
                return;
            }

            // ---- 房间由美术驱动（通用关卡器）----
            // RoomFolder 里每个「夜版前缀 + 名字」的 PNG 就是一件家具：名字直接取自文件名，
            // 所以加一张图 = 多一件可查的家具，不用改代码、也不需要每关的数据资产。
            // 位置不用手摆：这些图都是整帧 3840×2160，家具在哪、什么形状本来就在画里。
            Img(roomView, "RoomBackground", LoadSprite($"{RoomFolder}/{NightPrefix}{BackgroundKeyword}.png"), 0f, 0f, 1f, 1f);

            var furniture = new List<(string baseName, SampleInteractableRule rule)>();
            foreach (var layer in EnumerateFurnitureLayers())
            {
                var baseName = Path.GetFileNameWithoutExtension(layer);
                var displayName = StripVariantPrefix(baseName);

                // 每件家具 = 一整帧图层（只画该家具），图本身就是交互物：贴图开了 Read/Write + FullRect，
                // 用 alphaHitTestMinimumThreshold 只在不透明像素上吃射线（点空气点不到；重叠处谁不透明谁先接，
                // 透明处自动穿透到底下那件）。
                //
                // 这里**不写内容**：能用哪把工具、读到什么、这次是不是异常，全部由 CaseDirector 开局按种子灌进来
                // （见 SampleInteractableRule.ConfigureFromCase）—— 这就是"通用关卡器"的关键。
                var rule = CreateInteractable(roomView, $"obj_{baseName}", baseName, displayName,
                    FlavorDescription(displayName), FlavorAnomaly(displayName),
                    null, false, ToolType.None, string.Empty, string.Empty, string.Empty, 0);

                furniture.Add((baseName, rule));
            }

            if (furniture.Count == 0)
            {
                Debug.LogError($"[Villa] {RoomFolder} 里没有找到「{NightPrefix}*」的家具图层，房间是空的。");
                return;
            }

            Debug.Log($"[Villa] 房间按美术生成：{furniture.Count} 件家具（扫的是 {RoomFolder} 里 {NightPrefix}* 的图层）。");

            // 特写图层按名字绑到家具上（「X特写」→ 家具 X；现有美术名字对不上，见对照表）。
            BindCloseUps(furniture);

            // ---- 特写层：整帧不透明图，显示时盖住整个房间 ----
            // 必须是 BuildRooms 的最后一个子物体（= 最上层），否则会被家具图层压住。
            var closeUpGo = new GameObject("CloseUp", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            closeUpGo.transform.SetParent(roomView, false);
            Stretch((RectTransform)closeUpGo.transform);

            var closeUpImage = closeUpGo.GetComponent<Image>();
            closeUpImage.color = new Color(1f, 1f, 1f, 0f);
            closeUpImage.raycastTarget = false;
            // 一开始就没有特写图，直接别画（RoomCloseUpView 会在切过去时打开）。
            closeUpImage.enabled = false;

            var closeUpView = closeUpGo.AddComponent<RoomCloseUpView>();
            // HUD 挂在预制体实例根上，RoomView 是它的后代，往上找就行（省得改两个方法的签名）。
            var hudForCloseUp = roomView.GetComponentInParent<InvestigationHudView>();
            if (hudForCloseUp == null)
            {
                Debug.LogWarning("[Villa] 往上找不到 InvestigationHudView，特写层没接 HUD（检视时不会切特写）。");
            }

            var cso = new SerializedObject(closeUpView);
            cso.FindProperty("hud").objectReferenceValue = hudForCloseUp;
            cso.FindProperty("closeUpImage").objectReferenceValue = closeUpImage;
            cso.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[Villa] 特写层已建：RoomView/CloseUp（检视带 InteractableCloseUp 的家具时淡入，收起检视淡出）");
        }

        // ---------- 房间图层的扫描（美术即关卡）----------

        /// <summary>夜版的家具图层：排除底图与特写，按文件名排序（保证 z 序稳定、每局一致）。</summary>
        private static List<string> EnumerateFurnitureLayers()
        {
            var result = new List<string>();
            foreach (var file in EnumerateRoomLayers())
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.Contains(BackgroundKeyword) || name.Contains(CloseUpKeyword))
                {
                    continue;
                }

                result.Add(file);
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }

        /// <summary>特写图层。</summary>
        private static List<string> EnumerateCloseUpLayers()
        {
            var result = new List<string>();
            foreach (var file in EnumerateRoomLayers())
            {
                if (Path.GetFileNameWithoutExtension(file).Contains(CloseUpKeyword))
                {
                    result.Add(file);
                }
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }

        /// <summary>房间文件夹里符合夜版前缀的贴图（编辑器期扫描；运行时用的是生成好的场景）。</summary>
        private static IEnumerable<string> EnumerateRoomLayers()
        {
            if (!AssetDatabase.IsValidFolder(RoomFolder))
            {
                Debug.LogError($"[Villa] 房间文件夹不存在：{RoomFolder}");
                yield break;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { RoomFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileNameWithoutExtension(path).StartsWith(NightPrefix, StringComparison.Ordinal))
                {
                    continue; // 白天版（3a）不进关卡
                }

                yield return path;
            }
        }

        /// <summary>「3b吊灯」→「吊灯」：去掉夜版前缀，得到给玩家看的名字。</summary>
        private static string StripVariantPrefix(string baseName)
        {
            return baseName.StartsWith(NightPrefix, StringComparison.Ordinal)
                ? baseName.Substring(NightPrefix.Length)
                : baseName;
        }

        /// <summary>
        /// 把特写图层绑到家具上：`X特写` 归家具 `X`。
        /// 现有美术里特写叫「抽屉特写」、家具叫「木桌」，按约定匹配不上，所以有一张显式对照表。
        /// </summary>
        private static void BindCloseUps(List<(string baseName, SampleInteractableRule rule)> furniture)
        {
            foreach (var file in EnumerateCloseUpLayers())
            {
                var closeUpName = StripVariantPrefix(Path.GetFileNameWithoutExtension(file));
                var ownerKeyword = CloseUpOwnerOverrides.TryGetValue(closeUpName, out var mapped)
                    ? mapped
                    : closeUpName.Replace(CloseUpKeyword, string.Empty);

                var index = furniture.FindIndex(f => StripVariantPrefix(f.baseName).Contains(ownerKeyword));
                if (index < 0)
                {
                    Debug.LogWarning($"[Villa] 特写「{closeUpName}」在房间里找不到对应家具（关键词 {ownerKeyword}），没接上。");
                    continue;
                }

                var rule = furniture[index].rule;
                if (rule == null)
                {
                    continue;
                }

                var config = rule.gameObject.AddComponent<InteractableCloseUp>();
                var cso = new SerializedObject(config);
                cso.FindProperty("closeUpSprite").objectReferenceValue = LoadSprite(file);
                cso.ApplyModifiedPropertiesWithoutUndo();

                Debug.Log($"[Villa] 特写已接：{file} → {furniture[index].baseName}"
                          + "（点开这件家具的检视时切过去，点一下特写切回来；要不要先检视由案情随机决定）");
            }
        }

        /// <summary>家具"看上去是什么"。案情是随机的，所以这里只写外观、不写线索。</summary>
        private static string FlavorDescription(string displayName)
        {
            if (displayName.Contains("吊灯")) return "客厅正中的吊灯。没有通电，却还有一点余温。";
            if (displayName.Contains("书")) return "靠墙的一排书被抽出来过，书脊的裁口还是新的。";
            if (displayName.Contains("木桌")) return "靠窗的木桌，桌面很干净，抽屉却锁着。";
            if (displayName.Contains("沙发")) return "一张老式布艺沙发，坐垫边缘有被反复拆开过的痕迹。";
            if (displayName.Contains("茶几")) return "矮茶几上放着一只没喝完的杯子。";
            if (displayName.Contains("水渍")) return "地毯边缘有一小片深色痕迹，闻起来有点铁锈味。";
            return "房间里的一件普通家具，看不出什么特别。";
        }

        /// <summary>这件家具"盯着看有点不对"时的描述（详情区里异常那一行）。</summary>
        private static string FlavorAnomaly(string displayName)
        {
            if (displayName.Contains("吊灯")) return "钨丝在无供电状态下仍然发着极微弱的光。";
            if (displayName.Contains("书")) return "书页上的荧光指印连成了一条线。";
            if (displayName.Contains("木桌")) return "抽屉里的东西带着不正常的电磁读数。";
            if (displayName.Contains("沙发")) return "夹层里缝着一撮不属于这栋房子的人的头发。";
            if (displayName.Contains("茶几")) return "杯壁还是温的，可屋里已经没人了。";
            if (displayName.Contains("水渍")) return "在紫外线下，痕迹泛着不属于人类的冷光。";
            return "盯着看久了，会觉得它的位置不太对。";
        }

        /// <summary>
        /// 结算界面：挂在 Canvas 最上层的一个空节点上，**不预先摆界面** ——
        /// 组件会在玩家交出结论之后自己把那一屏搭出来（顺便借场景里已有的中文 TMP 字体）。
        /// 所以这里只放一个 RectTransform + 组件，节点本身必须保持 active（否则组件收不到 Update）。
        /// </summary>
        private static void CreateCaseResultPanel(Canvas canvas)
        {
            if (canvas == null)
            {
                Debug.LogWarning("[Villa] 没有 Canvas，结算界面没建。");
                return;
            }

            var go = new GameObject("CaseResult", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)go.transform);
            go.transform.SetAsLastSibling(); // 盖在所有东西上面
            go.AddComponent<CaseResultPanel>();

            Debug.Log("[Villa] 结算界面已建：Canvas/CaseResult（提交结论后自己搭出那一屏）");
        }

        /// <summary>
        /// 小软件窗口（SmallApp）= 玩法里的「笔记」：观测记录 + 结论选项都在这儿，`nothink` 回归纯详情区。
        ///
        /// 它**自带** <c>UIWindowManager</c>（`openedUIGroup` 已经接在它内部的 `openedUI` 上、`animationDuration 0.4`），
        /// 所以这里只负责：实例化 → 挂笔记视图 → 初始收起。
        /// 展开由左侧栏的 `setting_icon`（`ButtonAction.OpenWindow` → `FindObjectOfType&lt;UIWindowManager&gt;().Expand()`）触发。
        /// </summary>
        private static void CreateSmallApp(Canvas canvas)
        {
            if (canvas == null)
            {
                Debug.LogWarning("[Villa] 没有 Canvas，小软件窗口没建。");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SmallAppPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Villa] 找不到 {SmallAppPrefabPath}，笔记页没建出来。");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            instance.name = "SmallAppUI";
            Stretch((RectTransform)instance.transform);
            instance.transform.SetAsLastSibling(); // 窗口展开时要盖在玩法 UI 上面

            // 分页 + 笔记页 + 启动屏流程：预制体里 `Content` 下面**没有页面容器** ✗，
            // 只有启动屏和 6 个空按钮，所以全在这里新建并接线。
            var journalPage = CreateSmallAppPages(instance);

            // 初始收起：**不能关掉节点** ✗ —— OpenWindow 用的是 FindObjectOfType（不含未激活对象），
            // 关掉的话点 setting_icon 什么都不会发生。所以只把窗口的 CanvasGroup 透明度清 0。
            SetSmallAppCollapsed(instance);

            // 顺手把左侧栏的齿轮接成"打开小软件"（场景侧覆盖；预制体里那两个按钮只有名字、没有动作 ✗）。
            WireSmallAppButton(canvas);

            Debug.Log($"[Villa] 小软件窗口已建：{SmallAppPrefabPath} → SmallAppUI（setting_icon 展开，里面是笔记页）。");
        }

        /// <summary>把 SmallApp 收起来（只动透明度）。按类型名找它的 UIWindowManager，免得引 namespace。</summary>
        private static void SetSmallAppCollapsed(GameObject instance)
        {
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().Name != "UIWindowManager")
                {
                    continue;
                }

                var so = new SerializedObject(behaviour);
                var groupProp = so.FindProperty("openedUIGroup");
                var group = groupProp != null ? groupProp.objectReferenceValue as CanvasGroup : null;
                if (group != null)
                {
                    group.alpha = 0f;
                    group.blocksRaycasts = false;
                    Debug.Log("[Villa] 小软件窗口已收起（openedUIGroup.alpha = 0）。");
                }
                else
                {
                    Debug.LogWarning("[Villa] 小软件里的 UIWindowManager 没接 openedUIGroup，收起没做。");
                }

                return;
            }

            Debug.LogWarning("[Villa] 小软件预制体里没找到 UIWindowManager（OpenWindow 会无效）。");
        }

        /// <summary>
        /// 把左侧栏的 `setting_icon` 接成 `ButtonAction.OpenWindow`（= 展开 SmallApp/笔记）。
        ///
        /// 两个"不猜"的细节：
        /// - 用**类型名**找 `ButtonAction`（不引它的 namespace ✗，因为那个类在 Project.UI 下、本文件没引）；
        /// - 用 `enumNames` **按名字**找 `OpenWindow`（不猜枚举下标 ✗）。
        /// </summary>
        private static void WireSmallAppButton(Canvas canvas)
        {
            Transform settingIcon = null;
            foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "setting_icon")
                {
                    settingIcon = t;
                    break;
                }
            }

            if (settingIcon == null)
            {
                Debug.LogWarning("[Villa] 没找到 LeftApp/setting_icon，小软件窗口没法从左侧栏打开。");
                return;
            }

            foreach (var component in settingIcon.GetComponents<Component>())
            {
                if (component == null || component.GetType().Name != "ButtonAction")
                {
                    continue;
                }

                var so = new SerializedObject(component);
                var typeProp = so.FindProperty("actionType");
                if (typeProp != null)
                {
                    var names = typeProp.enumNames;
                    for (var i = 0; i < names.Length; i++)
                    {
                        if (names[i] == "OpenWindow")
                        {
                            typeProp.enumValueIndex = i;
                            break;
                        }
                    }
                }

                var targetProp = so.FindProperty("targetId");
                if (targetProp != null)
                {
                    targetProp.stringValue = string.Empty;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Villa] setting_icon 已接成 OpenWindow（点它展开 SmallApp = 笔记）。");
                return;
            }

            Debug.LogWarning("[Villa] setting_icon 上没有 ButtonAction，接不了 OpenWindow。");
        }

        /// <summary>按名字在子层级里找节点（含未激活）：SmallApp 里的 Content/openedUI/setting_icon 都靠它。</summary>
        private static RectTransform FindChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == childName)
                {
                    return t as RectTransform;
                }
            }

            return null;
        }

        /// <summary>
        /// 建小软件的分页并接线。预制体（导出层级确认过 ✓）里只有：
        /// 启动屏（`OKAS`/`system`/`System_2`/`peopleicon` + 「进入系统」按钮 `start`）
        /// 和菜单 `all_button`（2×3 网格，默认关闭）里的 6 个空按钮 ✗ —— `Content` 下面**没有页面容器** ✗。
        /// 所以这里新建 5 页、把按钮接上、并把笔记视图放进"笔记"页。返回笔记页。
        /// </summary>
        private static RectTransform CreateSmallAppPages(GameObject instance)
        {
            var content = FindChildByName(instance.transform, "Content");
            if (content == null)
            {
                Debug.LogWarning("[Villa] SmallApp 里没找到 Content，分页没建出来（笔记会退回默认版式）。");
                return null;
            }

            // 页名 ↔ 预制体里已有的按钮名（顺序照界面：上排 潜入/异常相册/道具，下排 设置/系统备份/退出系统）
            var pageTitles = new[] { "笔记", "线索清单", "道具", "设置", "案件" };
            var buttonNames = new[] { "潜入", "异常相册", "道具", "设置", "系统备份" };

            var pages = new RectTransform[pageTitles.Length];
            for (var i = 0; i < pageTitles.Length; i++)
            {
                var pageGo = new GameObject($"Page_{pageTitles[i]}", typeof(RectTransform));
                pageGo.transform.SetParent(content, false);

                var rect = (RectTransform)pageGo.transform;
                rect.anchorMin = new Vector2(0.03f, 0.03f);
                rect.anchorMax = new Vector2(0.97f, 0.97f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                pageGo.SetActive(false); // 显隐交给 SmallAppPageHost
                pages[i] = rect;
            }

            // 笔记视图：放进"笔记"页（从此不再挂在窗口根上 ✗，跟着页面一起显隐 ✓）
            var journal = instance.AddComponent<CaseJournalView>();
            var jso = new SerializedObject(journal);
            jso.FindProperty("minReadings").intValue = 1;
            jso.FindProperty("contentRoot").objectReferenceValue = pages[0];
            jso.ApplyModifiedPropertiesWithoutUndo();

            // 三页只读信息页：线索清单 / 道具 / 案件（各挂一个 CaseInfoPageView，模式不同）。
            // 剩下"设置"页由设置视图负责（照参考图那张 System Setting）。
            var infoPageIndexes = new[] { 1, 2, 4 };
            var infoModes = new[] { CasePageMode.Checklist, CasePageMode.Tools, CasePageMode.CaseInfo };
            for (var i = 0; i < infoPageIndexes.Length; i++)
            {
                var page = pages[infoPageIndexes[i]];
                if (page == null)
                {
                    continue;
                }

                var view = page.gameObject.AddComponent<CaseInfoPageView>();
                var vso = new SerializedObject(view);
                vso.FindProperty("mode").enumValueIndex = (int)infoModes[i];
                vso.ApplyModifiedPropertiesWithoutUndo();
            }

            // 设置页：照参考图 System Setting（显示模式 + 三条音量 + 返回），视图自己搭版式。
            if (pages[3] != null)
            {
                pages[3].gameObject.AddComponent<SettingsPageView>();
            }

            // 分页切换器（启动屏 → 进入系统 → 显示菜单与默认页）
            var host = instance.GetComponent<SmallAppPageHost>();
            if (host == null)
            {
                host = instance.AddComponent<SmallAppPageHost>();
            }

            var hso = new SerializedObject(host);
            AssignGameObjectArray(hso.FindProperty("startScreen"),
                FindChildByName(instance.transform, "OKAS"),
                FindChildByName(instance.transform, "system"),
                FindChildByName(instance.transform, "System_2"),
                FindChildByName(instance.transform, "peopleicon"));

            var startTransform = FindChildByName(instance.transform, "start");
            hso.FindProperty("startButton").objectReferenceValue =
                startTransform != null ? startTransform.GetComponent<Button>() : null;

            var menuTransform = FindChildByName(instance.transform, "all_button");
            hso.FindProperty("menuRoot").objectReferenceValue =
                menuTransform != null ? menuTransform.gameObject : null;

            var pagesProp = hso.FindProperty("pages");
            pagesProp.arraySize = pageTitles.Length;
            for (var i = 0; i < pageTitles.Length; i++)
            {
                var entry = pagesProp.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("title").stringValue = pageTitles[i];

                var buttonTransform = FindChildByName(instance.transform, buttonNames[i]);
                entry.FindPropertyRelative("button").objectReferenceValue =
                    buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
                entry.FindPropertyRelative("page").objectReferenceValue =
                    pages[i] != null ? pages[i].gameObject : null;
            }

            hso.FindProperty("defaultPage").intValue = 0;
            hso.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[Villa] 小软件分页已建：{string.Join(" / ", pageTitles)}"
                      + "（点「进入系统」→ 显示菜单与默认页" + "「笔记」；按钮↔页已接好）");

            return pages[0];
        }

        /// <summary>把若干 RectTransform 填进一个 GameObject[] 属性（逐个 GetArrayElementAtIndex，跨版本稳）。</summary>
        private static void AssignGameObjectArray(SerializedProperty arrayProp, params RectTransform[] items)
        {
            if (arrayProp == null)
            {
                return;
            }

            arrayProp.arraySize = items.Length;
            for (var i = 0; i < items.Length; i++)
            {
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue =
                    items[i] != null ? items[i].gameObject : null;
            }
        }

        private static ClueItem LoadClue(string assetName)
        {
            return AssetDatabase.LoadAssetAtPath<ClueItem>($"{ItemFolder}/{assetName}.asset");
        }

        private static SampleInteractableRule CreateInteractable(
            RectTransform roomView,
            string interactableId,
            string layerFile,
            string displayName,
            string description,
            string anomalyDescription,
            ClueItem clueItem,
            bool collectToContainment,
            ToolType requiredTool,
            string successFlag,
            string successText,
            string failureText,
            int failureSanityPenalty,
            bool registerEmf = false,
            int emfValue = 0,
            bool registerUv = false,
            bool uvValue = false)
        {
            var layerPath = $"{RoomFolder}/{layerFile}.png";

            // 图层图自己就是按钮：贴图开了 Read/Write（FixLivingRoomSpriteImport），
            // 用 alphaHitTestMinimumThreshold 只在不透明像素上吃射线，透明区域点不到。
            // 这样不必再放一个透明命中区（Hit_*），也不会出现"点空气点到桌子"的错判。
            var layerGo = new GameObject($"Obj_{displayName}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            layerGo.transform.SetParent(roomView, false);
            Stretch((RectTransform)layerGo.transform);

            var layer = layerGo.GetComponent<Image>();
            layer.sprite = LoadSprite(layerPath);
            layer.color = Color.white;
            layer.raycastTarget = true;
            layer.alphaHitTestMinimumThreshold = 0.5f;

            // 仍然量一下不透明范围：判定已经交给 alphaHitTest，这里只写日志，方便核对图和位置。
            var bounds = ComputeOpaqueBounds(layerPath);

            var interactable = layerGo.AddComponent<SimpleInteractable>();
            layerGo.AddComponent<SimpleInteractableAutoRegister>();
            var pointer = layerGo.AddComponent<SimpleInteractablePointerHandler>();
            var rule = layerGo.AddComponent<SampleInteractableRule>();
            var visual = layerGo.AddComponent<SimpleInteractableVisualState>();

            var iso = new SerializedObject(interactable);
            iso.FindProperty("interactableId").stringValue = interactableId;
            iso.FindProperty("description").stringValue = description;
            iso.FindProperty("anomalyDescription").stringValue = anomalyDescription;
            if (clueItem != null)
            {
                iso.FindProperty("associatedItem").objectReferenceValue = clueItem;
            }

            iso.ApplyModifiedPropertiesWithoutUndo();

            var pso = new SerializedObject(pointer);
            pso.FindProperty("highlightTarget").objectReferenceValue = layer;
            pso.ApplyModifiedPropertiesWithoutUndo();

            var rso = new SerializedObject(rule);
            rso.FindProperty("resolveOnClick").boolValue = requiredTool == ToolType.None;
            rso.FindProperty("requiredToolType").enumValueIndex = (int)requiredTool;
            rso.FindProperty("clueItem").objectReferenceValue = clueItem;
            rso.FindProperty("evidenceId").stringValue = clueItem != null ? clueItem.EvidenceId : string.Empty;
            rso.FindProperty("collectToContainment").boolValue = collectToContainment;
            rso.FindProperty("deactivateOnSuccess").boolValue = true;
            rso.FindProperty("markCollectedOnSuccess").boolValue = true;
            rso.FindProperty("successFlag").stringValue = successFlag;
            rso.FindProperty("successText").stringValue = successText;
            rso.FindProperty("failureText").stringValue = failureText;
            rso.FindProperty("failureSanityPenalty").intValue = failureSanityPenalty;
            rso.FindProperty("registerEmf").boolValue = registerEmf;
            rso.FindProperty("emfValue").intValue = emfValue;
            rso.FindProperty("registerUv").boolValue = registerUv;
            rso.FindProperty("uvValue").boolValue = uvValue;
            rso.ApplyModifiedPropertiesWithoutUndo();

            var vso = new SerializedObject(visual);
            vso.FindProperty("interactable").objectReferenceValue = interactable;
            vso.FindProperty("targetLayer").objectReferenceValue = layer;
            // 收集后要把点击关掉，现在命中的就是图层本身。
            vso.FindProperty("hitArea").objectReferenceValue = layer;
            vso.ApplyModifiedPropertiesWithoutUndo();

            // 悬停描边（淡蓝，和窗口 UI 一套）：整帧图的描边是 N 份偏移副本，
            // 所以组件默认 hoverOnly + generateOnAwake=false —— 只有鼠标停在上面时才长出来。
            layerGo.AddComponent<SpriteOutline>();

            Debug.Log($"[Villa] 交互物 {interactableId}（{displayName}）整帧图 alpha 命中，不透明范围 {bounds.xMin:0.###},{bounds.yMin:0.###} {bounds.width:0.###}×{bounds.height:0.###}");
            return rule;
        }

        /// <summary>
        /// 直接读 PNG 像素算不透明包围盒（归一化，y 从下往上）。
        /// 点击判定已经交给 Image.alphaHitTestMinimumThreshold（精确到像素），
        /// 这里量出来的范围只用来写日志，方便核对图有没有被裁掉或摆偏。
        /// </summary>
        private static Rect ComputeOpaqueBounds(string assetPath)
        {
            if (!File.Exists(assetPath))
            {
                Debug.LogWarning($"[Villa] 找不到贴图，日志里的不透明范围退化为整帧：{assetPath}");
                return new Rect(0f, 0f, 1f, 1f);
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.LoadImage(File.ReadAllBytes(assetPath));
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[Villa] 读取贴图失败，日志里的不透明范围退化为整帧：{assetPath}（{exception.Message}）");
                Object.DestroyImmediate(texture);
                return new Rect(0f, 0f, 1f, 1f);
            }

            var width = texture.width;
            var height = texture.height;
            var pixels = texture.GetPixels32();
            Object.DestroyImmediate(texture);

            const byte threshold = 8;
            const int step = 4; // 4K 图逐像素太慢，跳着扫足够准
            var minX = width;
            var minY = height;
            var maxX = -1;
            var maxY = -1;

            for (var y = 0; y < height; y += step)
            {
                var row = y * width;
                for (var x = 0; x < width; x += step)
                {
                    if (pixels[row + x].a <= threshold)
                    {
                        continue;
                    }

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0 || maxY < 0)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            const float padding = 0.008f;
            var x0 = Mathf.Clamp01((float)minX / width - padding);
            var y0 = Mathf.Clamp01((float)minY / height - padding);
            var x1 = Mathf.Clamp01((float)(maxX + 1) / width + padding);
            var y1 = Mathf.Clamp01((float)(maxY + 1) / height + padding);

            // 小物件（比如那排书只有整帧的 8%×6%）要保证有个最小可点面积，否则玩家很难点中。
            const float minSize = 0.04f;
            var boxWidth = Mathf.Max(x1 - x0, minSize);
            var boxHeight = Mathf.Max(y1 - y0, minSize);
            var center = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
            var boxX = Mathf.Clamp01(center.x - boxWidth * 0.5f);
            var boxY = Mathf.Clamp01(center.y - boxHeight * 0.5f);
            return new Rect(boxX, boxY, Mathf.Min(boxWidth, 1f - boxX), Mathf.Min(boxHeight, 1f - boxY));
        }

        // ---------- 通用 UI 帮助方法 ----------

        /// <summary>
        /// 归一化矩形（nx/ny 以**左上角**为原点）→ 锚点式 RectTransform。
        /// 用锚点而不是像素偏移：父级缩放/改分辨率都不会错位，也不会被父级尺寸影响。
        /// </summary>
        private static void AnchorRect(RectTransform rect, float nx, float ny, float nw, float nh)
        {
            rect.anchorMin = new Vector2(nx, 1f - ny - nh);
            rect.anchorMax = new Vector2(nx + nw, 1f - ny);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Image Img(Transform parent, string name, Sprite sprite, float nx, float ny, float nw, float nh)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            AnchorRect((RectTransform)go.transform, nx, ny, nw, nh);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>方向箭头：贴图本身朝上，按锚点摆一圈、用 Z 轴旋转指向四个方向。</summary>
        private static Image CreateArrow(Transform parent, string name, Sprite sprite, Vector2 anchor, float angleZ)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(24f, 24f);
            rect.localEulerAngles = new Vector3(0f, 0f, angleZ);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = new Color(1f, 1f, 1f, 0.2f);
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Txt(Transform parent, string name, string text, float nx, float ny, float nw, float nh, float fontSize, TextAnchor anchor, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            AnchorRect((RectTransform)go.transform, nx, ny, nw, nh);

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                tmp.font = font;
            }

            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = ToTmpAlignment(anchor);
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        private static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.TopLeft;
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        // ---------- 代码生成手势表盘的贴图 ----------

        /// <summary>生成一个朝上的白色实心三角（箭头用；其它方向靠旋转 Image）。</summary>
        private static Sprite EnsureArrowSprite()
        {
            var path = $"{GestureArtFolder}/DetroitArrow.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null && !ForceRebuildGestureArt)
            {
                return existing;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                // y 越大越靠上：顶部收成尖，底部最宽。
                var t = 1f - (float)y / (size - 1);
                var halfWidth = Mathf.Lerp(size * 0.5f * 0.85f, 0f, t);
                var centerX = (size - 1) * 0.5f;
                for (var x = 0; x < size; x++)
                {
                    var inside = Mathf.Abs(x - centerX) <= halfWidth;
                    // 1 像素软边
                    var soft = !inside && Mathf.Abs(x - centerX) <= halfWidth + 1f && t < 0.98f;
                    var alpha = inside ? (byte)255 : soft ? (byte)110 : (byte)0;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return WriteSceneSprite(texture, path);
        }

        /// <summary>生成一个白色圆环（进度表盘用；Image 会把它设成 Filled/Radial360）。</summary>
        private static Sprite EnsureRingSprite()
        {
            var path = $"{GestureArtFolder}/DetroitRing.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null && !ForceRebuildGestureArt)
            {
                return existing;
            }

            const int size = 256;
            const float thickness = 22f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var outer = size * 0.5f - 2f;
            var inner = outer - thickness;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha;
                    if (distance <= outer && distance >= inner)
                    {
                        alpha = 255;
                    }
                    else if ((distance > outer && distance <= outer + 1.5f) || (distance < inner && distance >= inner - 1.5f))
                    {
                        alpha = 110; // 软边，缩小后不锯齿
                    }
                    else
                    {
                        alpha = 0;
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return WriteSceneSprite(texture, path);
        }

        /// <summary>把运行时贴图写成工程里的 PNG 资源（Sprite / Single / 不压缩、双线性）。</summary>
        private static Sprite WriteSceneSprite(Texture2D texture, string assetPath)
        {
            var bytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            File.WriteAllBytes(assetPath, bytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            Debug.Log($"[Villa] 生成手势表盘贴图：{assetPath}（{(sprite != null ? "OK" : "加载失败")}）");
            return sprite;
        }

        /// <summary>生成雷达扫描用的扇形贴图（朝上、向右张开，前缘最亮、外缘略淡）。</summary>
        private static Sprite EnsureRadarSweepSprite()
        {
            var path = $"{GestureArtFolder}/RadarSweep.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null && !ForceRebuildGestureArt)
            {
                return existing;
            }

            const int size = 128;
            const float sweepDegrees = 70f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var radius = size * 0.5f - 1f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = 0;
                    if (distance <= radius)
                    {
                        // 0° = 正上方，顺时针为正。
                        var degree = Mathf.Repeat(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg, 360f);
                        if (degree <= sweepDegrees)
                        {
                            var strength = 1f - degree / sweepDegrees;   // 前缘最亮
                            var edgeFade = Mathf.Clamp01((radius - distance) / (radius * 0.15f) + 0.2f);
                            alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(strength * edgeFade));
                        }
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return WriteSceneSprite(texture, path);
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            // 不要用 Sprite.Create 兜底：运行时新建的 Sprite 不是资产，存不进场景，只会得到一个看似成功、
            // 实际全是空图的场景。这里直接报错，让问题暴露出来。
            Debug.LogError($"[Villa] 贴图没有 Sprite 子资源：{path}（跑一下 Tools/Project/Gameplay/Fix Living Room Sprite Import）");
            return null;
        }

        private static void AssignObjectArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values?.Length ?? 0;
            for (var i = 0; i < property.arraySize; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == ScenePath))
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[Villa] 已加入 Build Settings：{ScenePath}");
        }

        /// <summary>
        /// 把序章章节的结束行为接到本场景（LoadScene）。
        ///
        /// 为什么不直接写在章节资产里：本场景在跑这个工具之前**并不存在**，
        /// 提前把 endAction 改成 LoadScene 会让"打到序章结尾"变成加载失败 + 卡住。
        /// 所以改成"场景成功生成之后才接线"，两边不会脱节。
        /// </summary>
        private static void WirePrologueEndAction()
        {
            foreach (var path in new[] { PrologueChapterPath, PrologueChapterSourcePath })
            {
                var chapter = AssetDatabase.LoadAssetAtPath<VNChapterConfig>(path);
                if (chapter == null)
                {
                    Debug.LogWarning($"[Villa] 找不到序章章节资产，跳过接线：{path}");
                    continue;
                }

                var so = new SerializedObject(chapter);
                var endAction = so.FindProperty("endAction");
                if (endAction == null)
                {
                    Debug.LogWarning($"[Villa] {path} 里没有 endAction 字段，请手动把章节结束行为改成 Load Scene: {SceneName}");
                    continue;
                }

                // ⚠️ 这里**不切场景**：序幕 → 正式玩法的切换由 ProloguePerformanceDirector 驱动
                // （点完「开始潜入」→ 收对话框 → 等演出播完/mainUI 全屏 → 面板内过场切场景）。
                // 如果章节自己再切一次，就会切两遍。
                endAction.FindPropertyRelative("actionType").enumValueIndex = (int)VNEndActionType.None;
                endAction.FindPropertyRelative("targetSceneName").stringValue = SceneName;
                endAction.FindPropertyRelative("targetGameState").enumValueIndex = (int)GameState.Exploration;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(chapter);
                Debug.Log($"[Villa] 序章结束行为已设为「不切场景」（切场景交给 ProloguePerformanceDirector，目标 {SceneName}）：{path}");
            }

            AssetDatabase.SaveAssets();
        }
    }
}
