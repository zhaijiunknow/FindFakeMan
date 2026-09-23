using Project.UI.Scripts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.UI.Editor
{
    /// <summary>
    /// 把 VN 面板里的快进 / 自动播放按钮接到 VnSceneUiView 上。
    ///
    /// 菜单：Tools/Project/UI/Wire VN Skip/Auto Buttons
    ///
    /// 按钮在 VnSceneUiView.vnPanel 的子树里按名字找：先精确匹配（忽略大小写），
    /// 再退化成"名字里包含"匹配，所以 skip / Skip / SkipButton / auto / AutoButton 都能认。
    /// </summary>
    public static class WireVnControlButtons
    {
        private const string ScenePath = "Assets/Project/UI/Scenes/Px2050_Prologue.unity";

        [MenuItem("Tools/Project/UI/Wire VN Skip/Auto Buttons")]
        public static void Run()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.Log("[VN] 已取消：当前场景有未保存修改，操作中止。");
                    return;
                }

                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var view = FindViewInScene(scene);
            if (view == null)
            {
                Debug.LogError($"[VN] 场景里没有 VnSceneUiView：{ScenePath}");
                return;
            }

            var viewSerialized = new SerializedObject(view);
            var skipProperty = viewSerialized.FindProperty("skipButton");
            var autoProperty = viewSerialized.FindProperty("autoButton");
            if (skipProperty == null || autoProperty == null)
            {
                Debug.LogError("[VN] VnSceneUiView 上没有 skipButton / autoButton 字段，请先让 Unity 编译新脚本。");
                return;
            }

            var panel = viewSerialized.FindProperty("vnPanel")?.objectReferenceValue as GameObject;
            if (panel == null)
            {
                Debug.LogError("[VN] VnSceneUiView.vnPanel 没配置，无法定位快进/自动按钮。");
                return;
            }

            var skipButton = FindButtonByName(panel.transform, "skip");
            var autoButton = FindButtonByName(panel.transform, "auto");
            if (skipButton == null || autoButton == null)
            {
                Debug.LogError($"[VN] 在 {panel.name} 子树里找不到按钮（skip={(skipButton != null)}, auto={(autoButton != null)}）。");
                return;
            }

            skipProperty.objectReferenceValue = skipButton;
            autoProperty.objectReferenceValue = autoButton;
            viewSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[VN] 已接好快进按钮「{skipButton.name}」与自动按钮「{autoButton.name}」，并保存场景：{ScenePath}");
        }

        private static VnSceneUiView FindViewInScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var view = root.GetComponentInChildren<VnSceneUiView>(true);
                if (view != null)
                {
                    return view;
                }
            }

            return null;
        }

        private static Button FindButtonByName(Transform root, string keyword)
        {
            Button partial = null;
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var objectName = button.gameObject.name;
                if (string.Equals(objectName, keyword, System.StringComparison.OrdinalIgnoreCase))
                {
                    return button;
                }

                if (partial == null && objectName.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    partial = button;
                }
            }

            return partial;
        }
    }
}
