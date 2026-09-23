using System;
using System.Linq;
using Project.Narrative.Scripts;
using Project.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Editor
{
    /// <summary>
    /// 在 Px2050_Prologue 场景搭建序章开场演出并接线：
    ///   - 复用/创建 全屏黑层(BlackOverlay) 与 CRT 扫描线层(ScreenFx)——已存在就复用，
    ///     不重建，避免打断别处（例如终端过场）对它们的引用；
    ///   - 房间只有一层：把它的贴图设成**亮版 扣a3**，并把**夜版 扣a1** 接给导演，
    ///     推远结束时导演直接换 sprite（不做 alpha）；
    ///   - 清掉早期版本留下的 RoomBright / RoomNight 叠层；
    ///   - 重建 ProloguePerformanceDirector 并接好全部引用与参数。
    ///
    /// 菜单：Tools/Project/Prologue → Build Performance
    /// </summary>
    public static class BuildProloguePerformance
    {
        private const int UiLayer = 5;
        private const string BackgroundName = "background";
        private const string NewsName = "2";
        private const string BrightRoomSpritePath = "Assets/Project/Resource/Indoor/扣a3.png";
        private const string NightRoomSpritePath = "Assets/Project/Resource/Indoor/扣a1.png";
        private static readonly string[] LegacyLayerNames = { "RoomBright", "RoomNight" };

        [MenuItem("Tools/Prologue/Build Performance")]
        public static void Build()
        {
            var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c != null && c.isRootCanvas);
            if (canvas == null)
            {
                Debug.LogWarning("[Prologue] 未找到根 Canvas。");
                return;
            }

            var background = canvas.transform.Find(BackgroundName);
            if (background == null)
            {
                Debug.LogWarning($"[Prologue] 未找到 Canvas/{BackgroundName}。");
                return;
            }

            var newsImage = background.Find(NewsName)?.GetComponent<Image>();
            if (newsImage == null)
            {
                Debug.LogWarning($"[Prologue] 未找到 Canvas/{BackgroundName}/{NewsName}。");
                return;
            }

            // 清掉早期版本留下的房间叠层（现在是"单层换 sprite"，不需要额外层）。
            foreach (var legacyName in LegacyLayerNames)
            {
                var legacy = background.Find(legacyName);
                if (legacy != null)
                {
                    UnityEngine.Object.DestroyImmediate(legacy.gameObject);
                    Debug.Log($"[Prologue] 已删除早期版本留下的 {legacyName} 层。");
                }
            }

            // 唯一的房间层：默认设成亮版 扣a3。
            var brightSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BrightRoomSpritePath);
            var nightSprite = AssetDatabase.LoadAssetAtPath<Sprite>(NightRoomSpritePath);
            var roomImage = FindRoomImage(background);

            if (roomImage == null)
            {
                Debug.LogWarning($"[Prologue] {BackgroundName} 下没找到用\"扣a*\"贴图的房间层。");
            }
            else if (brightSprite == null)
            {
                Debug.LogWarning($"[Prologue] 找不到亮版房间贴图：{BrightRoomSpritePath}");
            }
            else if (roomImage.sprite != brightSprite)
            {
                roomImage.sprite = brightSprite;
                roomImage.color = new Color(roomImage.color.r, roomImage.color.g, roomImage.color.b, 1f);
                Debug.Log($"[Prologue] 房间层「{roomImage.name}」的贴图已设为 扣a3（默认亮版）。");
            }

            // 复用已存在的黑幕/扫描线（不重建，避免打断别处的引用）。
            var blackGroup = FindCanvasGroup("BlackOverlay", canvas.transform, out var blackCreated);
            var fxOverlay = FindScreenFx(canvas.transform, out var fxCreated);

            // 导演：直接重建（没有别的东西引用它）。
            foreach (var existing in UnityEngine.Object.FindObjectsByType<ProloguePerformanceDirector>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            var dirGo = new GameObject("ProloguePerformanceDirector");
            var director = dirGo.AddComponent<ProloguePerformanceDirector>();
            var vnDirector = UnityEngine.Object.FindObjectsByType<VNDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault();

            var so = new SerializedObject(director);
            so.FindProperty("blackOverlay").objectReferenceValue = blackGroup;
            so.FindProperty("backgroundImage").objectReferenceValue = newsImage;
            so.FindProperty("screenFx").objectReferenceValue = fxOverlay;
            so.FindProperty("nightRoom").objectReferenceValue = roomImage;
            so.FindProperty("nightSprite").objectReferenceValue = nightSprite;
            so.FindProperty("vndirector").objectReferenceValue = vnDirector;

            // 现推荐的演出参数（序列化值优先，所以由工具来写）。
            so.FindProperty("crtStartNodeId").stringValue = "opening_news_003";
            so.FindProperty("pullStartNodeId").stringValue = "opening_news_004";
            so.FindProperty("startScale").floatValue = 1.4f;
            so.FindProperty("endScale").floatValue = 0.4f;
            so.FindProperty("pullDuration").floatValue = 2f;
            so.FindProperty("stopCrtAfterPull").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(canvas.gameObject);
            Debug.Log($"[Prologue] 序章演出已接线：黑幕{(blackCreated ? "(新建)" : "(复用)")}、" +
                      $"CRT{(fxCreated ? "(新建)" : "(复用)")}、房间层={(roomImage != null ? roomImage.name : "无")}" +
                      $"（默认 {brightSprite?.name ?? "?"} → 推远后换 {nightSprite?.name ?? "?"}）、" +
                      $"VNDirector={(vnDirector != null ? "已接" : "未找到(走 GameServices)")}。" +
                      " 参数：1.4 → 0.4 / 2s。");
        }

        /// <summary>房间层 = 背景里最后一个用"扣a*"贴图的 Image。</summary>
        private static Image FindRoomImage(Transform background)
        {
            Image found = null;
            foreach (var image in background.GetComponentsInChildren<Image>(true))
            {
                var sprite = image.sprite;
                if (sprite == null || !sprite.name.StartsWith("扣a", StringComparison.Ordinal))
                {
                    continue;
                }

                found = image;
            }

            return found;
        }

        private static CanvasGroup FindCanvasGroup(string name, Transform canvas, out bool created)
        {
            var existing = canvas.Find(name);
            if (existing != null)
            {
                created = false;
                var group = existing.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = existing.gameObject.AddComponent<CanvasGroup>();
                }

                return group;
            }

            created = true;
            return CreateOverlay(name, canvas, Color.black).GetComponent<CanvasGroup>();
        }

        private static ScreenFxOverlay FindScreenFx(Transform canvas, out bool created)
        {
            var existing = canvas.Find("ScreenFx");
            if (existing != null)
            {
                created = false;
                var overlay = existing.GetComponent<ScreenFxOverlay>();
                if (overlay == null)
                {
                    overlay = existing.gameObject.AddComponent<ScreenFxOverlay>();
                }

                return overlay;
            }

            created = true;
            var go = CreateOverlay("ScreenFx", canvas, Color.white);
            go.SetActive(false);
            return go.AddComponent<ScreenFxOverlay>();
        }

        private static GameObject CreateOverlay(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.layer = UiLayer;
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            return go;
        }
    }
}
