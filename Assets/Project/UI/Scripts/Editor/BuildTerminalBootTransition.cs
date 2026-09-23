using System;
using Project.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.UI.Editor
{
    /// <summary>
    /// 搭好序幕那段「关电脑屏幕 → 开机自检 → 打开终端 → 眨眼全屏」过场需要的场景物件并接线。
    ///
    /// 菜单：Tools/Project/UI/Build Terminal Boot Transition
    ///
    /// 关键约定（口径：镜头不动、位置不变、加载占满 2）：
    ///  - 关机只关 Canvas/background/2（新闻画面 = 主角的电脑屏幕），房间(扣a2)与深色底都不动；
    ///  - ScreenContent 是 2 的兄弟节点、同一个全屏 rect → 加载与屏幕内终端的**大小和可见范围与 2 完全一致**
    ///    （不做任何缩放/平移，也不做显示器镂空检测）；
    ///  - 白线也在 ScreenContent 里，所以关机的收屏线出现在新闻的位置上；
    ///  - 只有眨眼的上下黑边是全屏的，眨眼全黑那一瞬 mainUI 被提到 Canvas 顶层变成全屏。
    ///
    /// 会做这些事（可重复执行）：
    ///  1. 找到 background / 2 / 扣a2 / ScreenFx / Canvas；
    ///  2. 建/复用 MainUI（Main.prefab 实例），初始隐藏；
    ///  3. 在 background 下建 ScreenContent（插在 扣a2 之前），里面建 BootScreen（黑底 + 自检文字）与 WhiteLine；
    ///  4. 在 Canvas 下建 TerminalTransition（最上层）：BlinkTop + BlinkBottom；
    ///  5. 读 扣a2 贴图的 alpha 通道，算出「显示器镂空」的归一化矩形写进组件；
    ///  6. 接线 + 调整层级 + 保存场景。
    /// </summary>
    public static class BuildTerminalBootTransition
    {
        private const string ScenePath = "Assets/Project/UI/Scenes/Px2050_Prologue.unity";
        private const string MainPrefabPath = "Assets/Project/UI/Prefabs/Main.prefab";
        private const string TriggerNodeId = "briefing_whitewan_001";
        private const string RootName = "TerminalTransition";
        private const string ScreenContentName = "ScreenContent";
        private const string RoomObjectName = "扣a2";
        private const string NewsObjectName = "2";

        private static readonly Color BarColor = new Color(0.031f, 0.031f, 0.039f, 1f);
        private static readonly Color BootBackColor = new Color(0.02f, 0.024f, 0.031f, 1f);
        private static readonly Color BootTextColor = new Color(0.81f, 0.89f, 1f, 1f);

        [MenuItem("Tools/Project/UI/Build Terminal Boot Transition")]
        public static void Build()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.Log("[Boot] 已取消：当前场景有未保存修改，操作中止。");
                    return;
                }

                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var canvas = FindRootCanvas();
            if (canvas == null)
            {
                Debug.LogError($"[Boot] {ScenePath} 里找不到根 Canvas。");
                return;
            }

            var canvasRect = (RectTransform)canvas.transform;
            var roomRoot = canvasRect.Find("background") as RectTransform;
            if (roomRoot == null)
            {
                Debug.LogError("[Boot] 找不到 Canvas/background。");
                return;
            }

            var newsVisual = roomRoot.Find(NewsObjectName) as RectTransform;
            if (newsVisual == null)
            {
                Debug.LogError($"[Boot] 找不到 {roomRoot.name}/{NewsObjectName}（新闻画面）。");
                return;
            }

            var roomImage = roomRoot.Find(RoomObjectName) as RectTransform;
            if (roomImage == null)
            {
                Debug.LogWarning($"[Boot] 找不到 {roomRoot.name}/{RoomObjectName}（房间图）；ScreenContent 会放到最后，" +
                                 "也就是会盖在房间之上（可能不再被房间的镂空裁切）。");
            }

            var crtFx = UnityEngine.Object.FindFirstObjectByType<ScreenFxOverlay>(FindObjectsInactive.Include);
            var mainUi = FindOrCreateMainUi(canvasRect);

            // 旧的过场物件整体重建。
            var existingRoot = canvasRect.Find(RootName);
            if (existingRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(existingRoot.gameObject);
            }

            var screenContent = CreateScreenContent(roomRoot, roomImage);
            var bootScreen = CreateBootScreen(screenContent, out var bootLog);
            // 白线也放在内容层里：它和 2 同一个 rect，所以关机的收屏线出现在新闻那个位置上。
            var whiteLine = CreateWhiteLine(screenContent);

            var rootRect = CreateRoot(canvasRect);
            var blinkTop = CreateBlinkBar(rootRect, "BlinkTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            var blinkBottom = CreateBlinkBar(rootRect, "BlinkBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));

            var transition = rootRect.gameObject.AddComponent<TerminalBootTransition>();
            var so = new SerializedObject(transition);
            so.FindProperty("triggerNodeId").stringValue = TriggerNodeId;
            so.FindProperty("newsVisual").objectReferenceValue = newsVisual;
            so.FindProperty("screenContent").objectReferenceValue = screenContent;
            so.FindProperty("mainUiRoot").objectReferenceValue = mainUi;
            so.FindProperty("canvasRoot").objectReferenceValue = canvasRect;
            so.FindProperty("whiteLine").objectReferenceValue = whiteLine;
            so.FindProperty("whiteLineGroup").objectReferenceValue = whiteLine.GetComponent<CanvasGroup>();
            so.FindProperty("crtFx").objectReferenceValue = crtFx;
            so.FindProperty("bootRoot").objectReferenceValue = bootScreen;
            so.FindProperty("bootLogText").objectReferenceValue = bootLog;
            so.FindProperty("blinkTopBar").objectReferenceValue = blinkTop;
            so.FindProperty("blinkBottomBar").objectReferenceValue = blinkBottom;

            // 顺手把自检节奏写成当前推荐值（场景里已序列化的旧值不会随代码默认值更新）。
            // 注意：这段过场不锁剧情推进，所以节奏只是观感问题，随时可在 Inspector 调。
            so.FindProperty("bootCharInterval").floatValue = 0.012f;
            so.FindProperty("bootLineInterval").floatValue = 0.08f;
            so.ApplyModifiedPropertiesWithoutUndo();

            bootScreen.SetActive(false);
            whiteLine.GetComponent<CanvasGroup>().alpha = 0f;

            OrderCanvas(canvasRect, roomRoot, mainUi, rootRect);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Boot] 过场已就绪：根={RootName}，只关 {roomRoot.name}/{newsVisual.name}，" +
                      $"开机层={ScreenContentName}/{bootScreen.name}，终端={(mainUi != null ? mainUi.name : "无")}，触发节点={TriggerNodeId}。");
        }

        // ---- 物件 ----

        /// <summary>屏幕内容层：插在 扣a2（房间）之前，这样内容会被房间的镂空裁出来。</summary>
        private static RectTransform CreateScreenContent(RectTransform roomRoot, RectTransform roomImage)
        {
            var existing = roomRoot.Find(ScreenContentName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject(ScreenContentName, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(roomRoot, false);
            Stretch(rect);

            if (roomImage != null)
            {
                rect.SetSiblingIndex(roomImage.GetSiblingIndex());
            }
            else
            {
                rect.SetAsLastSibling();
            }

            return rect;
        }

        private static GameObject CreateBootScreen(RectTransform parent, out TMP_Text logText)
        {
            var root = new GameObject("BootScreen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            Stretch(rect);

            var background = root.GetComponent<Image>();
            background.color = BootBackColor;
            // 纯视觉层，不挡点击：过场不干涉剧情，玩家随时可以点推进。
            background.raycastTarget = false;

            var logGo = new GameObject("BootLog", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var logRect = (RectTransform)logGo.transform;
            logRect.SetParent(rect, false);
            logRect.anchorMin = Vector2.zero;
            logRect.anchorMax = Vector2.one;
            logRect.offsetMin = new Vector2(80f, 60f);
            logRect.offsetMax = new Vector2(-80f, -60f);

            var tmp = logGo.GetComponent<TextMeshProUGUI>();
            var font = ApplyChineseFontToVnTexts.LoadOrCreateFontAsset();
            if (font != null)
            {
                tmp.font = font;
            }

            tmp.fontSize = 30f;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.color = BootTextColor;
            tmp.raycastTarget = false;
            tmp.text = string.Empty;

            logText = tmp;
            return root;
        }

        private static RectTransform CreateRoot(RectTransform canvasRect)
        {
            var go = new GameObject(RootName, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvasRect, false);
            Stretch(rect);
            return rect;
        }

        private static RectTransform CreateWhiteLine(RectTransform parent)
        {
            var go = new GameObject("WhiteLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 3f);

            var image = go.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            go.GetComponent<CanvasGroup>().alpha = 0f;
            return rect;
        }

        private static RectTransform CreateBlinkBar(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero; // 初始高度 0：看不见

            var image = go.GetComponent<Image>();
            image.color = BarColor;
            image.raycastTarget = false;
            return rect;
        }

        private static RectTransform FindOrCreateMainUi(RectTransform canvasRect)
        {
            for (var i = 0; i < canvasRect.childCount; i++)
            {
                var child = canvasRect.GetChild(i);
                if (child.name.StartsWith("Main", StringComparison.OrdinalIgnoreCase))
                {
                    child.gameObject.SetActive(false);
                    return child as RectTransform;
                }
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Boot] 找不到 {MainPrefabPath}，mainUiRoot 留空（过场到第 5 拍会没有终端可显示）。");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasRect);
            instance.name = "MainUI";
            instance.SetActive(false);
            Debug.Log($"[Boot] 场景里没有终端实例，已实例化 {MainPrefabPath} 并命名 MainUI。");
            return (RectTransform)instance.transform;
        }

        private static void OrderCanvas(RectTransform canvasRect, RectTransform roomRoot, RectTransform mainUi, RectTransform transitionRoot)
        {
            roomRoot.SetSiblingIndex(0);
            if (mainUi != null)
            {
                mainUi.SetSiblingIndex(1);
            }

            // 眨眼黑边要在最上（盖住房间与终端）。
            transitionRoot.SetSiblingIndex(canvasRect.childCount - 1);
        }

        private static Canvas FindRootCanvas()
        {
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas != null && canvas.isRootCanvas)
                {
                    return canvas;
                }
            }

            return null;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
