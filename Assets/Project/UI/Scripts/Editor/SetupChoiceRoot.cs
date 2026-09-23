using Project.UI.Scripts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.UI.Editor
{
    /// <summary>
    /// 把选项容器改成「全屏区域」并设成居中竖排（柚子社那种）：
    ///  - 把 Choices 从 VNPanel 挪到 Canvas 下，四边 offset 0 全屏拉伸；
    ///  - 层级放到 VNPanel 之后一位（画在字幕条之上、眨眼黑边之下）；
    ///  - 写入推荐参数：横向对齐 = Center、纵向 = Middle、边距 120/60/120/60、间距 12。
    ///
    /// 菜单：Tools/Project/UI/Make Choice Root Fullscreen
    /// </summary>
    public static class SetupChoiceRoot
    {
        private const string ScenePath = "Assets/Project/UI/Scenes/Px2050_Prologue.unity";
        private const string ChoiceRootName = "Choices";
        private const string VnPanelName = "VNPanel";

        [MenuItem("Tools/Project/UI/Make Choice Root Fullscreen")]
        public static void Run()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.Log("[Choices] 已取消：当前场景有未保存修改，操作中止。");
                    return;
                }

                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var view = FindViewInScene(scene);
            if (view == null)
            {
                Debug.LogError($"[Choices] 场景里没有 VnSceneUiView：{ScenePath}");
                return;
            }

            var canvas = FindRootCanvas();
            if (canvas == null)
            {
                Debug.LogError("[Choices] 找不到根 Canvas。");
                return;
            }

            var canvasRect = (RectTransform)canvas.transform;
            var viewSerialized = new SerializedObject(view);
            var rootProperty = viewSerialized.FindProperty("choiceRoot");

            var choiceRoot = rootProperty?.objectReferenceValue as RectTransform;
            if (choiceRoot == null)
            {
                choiceRoot = canvasRect.Find(ChoiceRootName) as RectTransform;
            }

            if (choiceRoot == null)
            {
                Debug.LogError($"[Choices] 找不到选项容器（VnSceneUiView.choiceRoot 为空，Canvas 下也没有 {ChoiceRootName}）。");
                return;
            }

            // 挪到 Canvas 下并铺满（全屏拉伸）。
            choiceRoot.SetParent(canvasRect, false);
            choiceRoot.anchorMin = Vector2.zero;
            choiceRoot.anchorMax = Vector2.one;
            choiceRoot.offsetMin = Vector2.zero;
            choiceRoot.offsetMax = Vector2.zero;
            choiceRoot.pivot = new Vector2(0.5f, 0.5f);
            choiceRoot.anchoredPosition = Vector2.zero;
            choiceRoot.localScale = Vector3.one;

            // 摆在 VNPanel 之后一位：盖住字幕条，但仍在眨眼黑边之下。
            var vnPanel = canvasRect.Find(VnPanelName);
            if (vnPanel != null)
            {
                choiceRoot.SetSiblingIndex(vnPanel.GetSiblingIndex() + 1);
            }

            if (rootProperty != null)
            {
                rootProperty.objectReferenceValue = choiceRoot;
            }

            // 居中竖排（柚子社风格）的推荐参数。
            var alignProperty = viewSerialized.FindProperty("choiceAlign");
            var verticalProperty = viewSerialized.FindProperty("choiceVertical");
            var marginProperty = viewSerialized.FindProperty("choiceMargin");
            var spacingProperty = viewSerialized.FindProperty("choiceButtonSpacing");

            if (alignProperty != null)
            {
                alignProperty.enumValueIndex = (int)VnSceneUiView.ChoiceAlign.Center;
            }

            if (verticalProperty != null)
            {
                verticalProperty.enumValueIndex = (int)VnSceneUiView.ChoiceVertical.Middle;
            }

            if (marginProperty != null)
            {
                marginProperty.vector4Value = new Vector4(120f, 60f, 120f, 60f);
            }

            if (spacingProperty != null)
            {
                spacingProperty.floatValue = 40f;
            }

            // 高度改成场景里可控：>0 直接覆盖预制体高度（不用再 Rebuild 预制体）。
            var heightProperty = viewSerialized.FindProperty("choiceButtonHeight");
            if (heightProperty != null)
            {
                heightProperty.floatValue = 60f;
            }

            // 字号：显式指定（>0 优先于"按高度自动"）。
            var fontSizeProperty = viewSerialized.FindProperty("choiceFontSize");
            if (fontSizeProperty != null)
            {
                fontSizeProperty.floatValue = 35f;
            }

            // 按钮大小按文字自适应：统一用最长那条的宽度，高度也会跟着换行长高。
            var widthModeProperty = viewSerialized.FindProperty("choiceWidthMode");
            var textPaddingProperty = viewSerialized.FindProperty("choiceTextPadding");
            var minWidthProperty = viewSerialized.FindProperty("choiceMinWidth");
            var maxWidthProperty = viewSerialized.FindProperty("choiceMaxWidth");

            if (widthModeProperty != null)
            {
                widthModeProperty.enumValueIndex = (int)VnSceneUiView.ChoiceWidthMode.UniformFitText;
            }

            if (textPaddingProperty != null)
            {
                textPaddingProperty.floatValue = 32f;
            }

            if (minWidthProperty != null)
            {
                minWidthProperty.floatValue = 860f;
            }

            if (maxWidthProperty != null)
            {
                maxWidthProperty.floatValue = 1200f;
            }

            // 打开布局日志：跑起来能直接从 Console 看到"文字宽 → 最终按钮尺寸"。
            var logProperty = viewSerialized.FindProperty("logChoiceLayout");
            if (logProperty != null)
            {
                logProperty.boolValue = true;
            }

            viewSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Choices] 「{choiceRoot.name}」已改成全屏区域（Canvas 直挂 + 四边 0），" +
                      $"并设为居中竖排 + 宽度自适应：Center / Middle / 边距 120,60,120,60 / " +
                      $"间距 40 / 高度 60 / 字号 35 / UniformFitText（额外留白 32，860~1200）。场景已保存：{ScenePath}");
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
    }
}
