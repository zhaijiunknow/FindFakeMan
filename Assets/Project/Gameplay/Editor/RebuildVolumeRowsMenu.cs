using Project.Core.Runtime.Managers;
using Project.UI.BigApp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.ProjectTools
{
    /// <summary>
    /// **只重建设置页那三行音量** ✓，在**当前打开的场景**里跑 ✓ —— 不重建整个场景 ✓。
    ///
    /// 用途：设置页的三行是构建器烘进场景的 ✓，场景里现存的还是旧的"没有圆点的横条"结构 ✗；
    /// 而"整个场景重建"会把你在场景里手调过的一切冲掉 ✗（按钮位置 ✓ 描边参数 ✓ 随机范围四角 ✓ …）。
    /// 这个菜单只做一件事：删掉旧的三行、按新结构（标准 `Slider` + `slide.png` 圆点 ✓）重搭 ✓。
    ///
    /// 菜单：`Tools/Project/UI/Rebuild Volume Rows (current scene)`
    /// </summary>
    public static class RebuildVolumeRowsMenu
    {
        /// <summary>滑块圆点的图 ✓（和构建器用的是同一张 ✓）。</summary>
        private const string HandleSpritePath = "Assets/Project/Resource/UI/设置/slide.png";

        [MenuItem("Tools/Project/UI/Rebuild Volume Rows (current scene)")]
        private static void RebuildVolumeRows()
        {
            // 含未激活 ✓ —— 设置页默认就是收着的（页面本体 SetActive(false) ✓）。
            var views = Object.FindObjectsByType<SettingsPageView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (views == null || views.Length == 0)
            {
                Debug.LogWarning("[VolumeRows] 当前场景里没有 SettingsPageView ✗ —— 先打开 Px2050_Villa 再跑这个菜单 ✓。");
                return;
            }

            var handleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HandleSpritePath);
            if (handleSprite == null)
            {
                Debug.LogWarning($"[VolumeRows] 找不到滑块圆点图：{HandleSpritePath} ✗（圆点会是空白 ✓）。");
            }

            var count = 0;
            foreach (var view in views)
            {
                if (view == null)
                {
                    continue;
                }

                // 圆点图必须在 RebuildVolumeRows() **之前**塞进去 ✓ —— 那三行是它内部搭的 ✓，
                // 搭完再给图就晚了 ✗（运行时脚本不能用 AssetDatabase ✗，只能这里喂 ✓）。
                var so = new SerializedObject(view);
                var spriteProp = so.FindProperty("sliderHandleSprite");
                if (spriteProp != null && handleSprite != null)
                {
                    spriteProp.objectReferenceValue = handleSprite;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                view.RebuildVolumeRows();
                EditorUtility.SetDirty(view);
                count++;

                var scene = view.gameObject.scene;
                if (scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            Debug.Log($"[VolumeRows] 已按新结构重建 {count} 个设置页的三行音量 ✓"
                      + "（标准 Slider + slide.png 圆点 ✓）—— 记得保存场景 ✓。");
        }
    }
}
