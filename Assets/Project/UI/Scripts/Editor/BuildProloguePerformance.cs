using System.Linq;
using Project.Narrative.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Editor
{
    /// <summary>
    /// 在 Px2050_Prologue 场景搭建序章演出：加全屏黑层(最上) + CRT 扫描线层 + 演出导演，接线到背景"2"。
    /// 用法：Tools → Prologue → Build Performance。需在 Px2050_Prologue 场景执行。
    /// </summary>
    public static class BuildProloguePerformance
    {
        private const int UiLayer = 5;

        [MenuItem("Tools/Prologue/Build Performance")]
        public static void Build()
        {
            var canvas = Object.FindObjectsOfType<Canvas>(true).FirstOrDefault();
            if (canvas == null)
            {
                Debug.LogWarning("[Prologue] 未找到 Canvas。");
                return;
            }

            var background = FindChild(canvas.transform, "background")?.Find("2")?.GetComponent<Image>();
            if (background == null)
            {
                Debug.LogWarning("[Prologue] 未找到 background/2。");
                return;
            }

            // 幂等：先移除旧的演出对象（重跑不重复）。
            foreach (var name in new[] { "BlackOverlay", "ScreenFx", "ProloguePerformanceDirector" })
            {
                foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(g => g.name == name))
                {
                    Object.DestroyImmediate(go);
                }
            }

            // CRT 扫描线层（先建，z 在 black 之下；初始隐藏）
            var fx = CreateOverlay("ScreenFx", canvas.transform, Color.white);
            var fxOverlay = fx.AddComponent<ScreenFxOverlay>();
            fx.SetActive(false);

            // 黑屏层（后建，z 在最上；初始 alpha=1 遮住背景）
            var black = CreateOverlay("BlackOverlay", canvas.transform, Color.black);
            var blackGroup = black.GetComponent<CanvasGroup>();
            blackGroup.alpha = 1f;

            var vnDirector = Object.FindObjectsByType<VNDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            Debug.Log($"[Prologue] FindObjectsByType<VNDirector> -> {(vnDirector != null ? "found" : "NULL")}");

            var dirGo = new GameObject("ProloguePerformanceDirector");
            var dir = dirGo.AddComponent<ProloguePerformanceDirector>();
            var so = new SerializedObject(dir);
            so.FindProperty("blackOverlay").objectReferenceValue = blackGroup;
            so.FindProperty("backgroundImage").objectReferenceValue = background;
            so.FindProperty("screenFx").objectReferenceValue = fxOverlay;
            so.FindProperty("vndirector").objectReferenceValue = vnDirector;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(canvas.gameObject);
            Debug.Log("[Prologue] 序章演出已接线（黑屏/CRT/导演 → background/2）。");
        }

        private static GameObject CreateOverlay(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.layer = UiLayer;
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling(); // 盖在最上（screen fx 先、black 后 → black 在上）

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

        private static Transform FindChild(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }
    }
}
