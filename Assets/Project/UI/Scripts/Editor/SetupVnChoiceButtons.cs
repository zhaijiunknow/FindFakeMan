using Project.UI.Scripts;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.UI.Editor
{
    /// <summary>
    /// VN 交互控件工具：
    /// 1. 按项目既有的"深色圆角板 + 柔光描边"语言生成统一风格的选项按钮预制体；
    /// 2. 把序幕场景的 VnSceneUiView 接好 choiceRoot / choiceButtonPrefab / choiceButtonSprite；
    /// 3. 删掉场景里旧的固定选项按钮（ChoiceButton1..N）。
    ///
    /// 菜单：
    /// - Tools/Project/UI/Setup VN Choice Buttons          已存在就不重建，只接线（缺底板图会先生成）
    /// - Tools/Project/UI/Rebuild VN Choice Button Prefab  强制按当前底板图重建
    ///
    /// 风格约定：
    /// - 底板是九宫格（见 GenerateVnChoiceArt），所以按钮可以任意拉宽而不变形；
    /// - 普通态 = 深色板 + 细边；悬停/选中 = 淡入蓝色柔光描边（同 小软件/select_button 的语义）；
    /// - 按下只给轻微压暗，不和柔光抢反馈；
    /// - 文字用中文字体资产，白字、居中、自动换行。
    /// </summary>
    public static class SetupVnChoiceButtons
    {
        /// <summary>选项按钮预制体路径（场景生成器也会读它）。</summary>
        public const string PrefabPath = "Assets/Project/UI/Prefabs/VNChoiceButton.prefab";

        /// <summary>选项按钮底板图（程序生成，美术出图后覆盖同名 PNG 即可）。</summary>
        public const string ChoicePlatePath = GenerateVnChoiceArt.PlatePath;

        /// <summary>选项按钮柔光描边图（悬停/选中态）。</summary>
        public const string ChoiceGlowPath = GenerateVnChoiceArt.GlowPath;

        /// <summary>VN 跳过按钮图标。</summary>
        public const string SkipPlatePath = "Assets/Project/Resource/UI/VN/skip.png";

        /// <summary>VN 自动播放按钮图标。</summary>
        public const string AutoPlatePath = "Assets/Project/Resource/UI/VN/auto.png";

        /// <summary>选项按钮默认尺寸（九宫格底板；宽度通常会被 VnSceneUiView 的文字自适应覆盖）。</summary>
        public static readonly Vector2 ChoiceButtonSize = new Vector2(720f, 48f);

        private const string ScenePath = "Assets/Project/UI/Scenes/Px2050_Prologue.unity";
        private const string ChoiceRootName = "Choices";
        private const string LegacyButtonPrefix = "ChoiceButton";

        [MenuItem("Tools/Project/UI/Setup VN Choice Buttons")]
        public static void Setup()
        {
            var prefab = CreateChoiceButtonPrefab(false);
            if (prefab != null)
            {
                WireScene(prefab);
            }
        }

        [MenuItem("Tools/Project/UI/Rebuild VN Choice Button Prefab")]
        public static void Rebuild()
        {
            var prefab = CreateChoiceButtonPrefab(true);
            if (prefab != null)
            {
                WireScene(prefab);
            }
        }

        /// <summary>
        /// 生成选项按钮预制体。已存在且 <paramref name="forceRebuild"/> 为 false 时直接复用。
        /// </summary>
        public static Button CreateChoiceButtonPrefab(bool forceRebuild)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Button>(PrefabPath);
            if (existing != null && !forceRebuild)
            {
                return existing;
            }

            if (existing != null)
            {
                AssetDatabase.DeleteAsset(PrefabPath);
            }

            GenerateVnChoiceArt.GenerateIfMissing();

            var plate = AssetDatabase.LoadAssetAtPath<Sprite>(ChoicePlatePath);
            var glow = AssetDatabase.LoadAssetAtPath<Sprite>(ChoiceGlowPath);
            if (plate == null)
            {
                Debug.LogWarning($"[VN] 没找到选项底板图 {ChoicePlatePath}，将用纯色兜底外观生成预制体。");
            }

            return BuildButtonPrefab("VNChoiceButton", plate, glow, ChoiceButtonSize, PrefabPath);
        }

        /// <summary>按同一套风格生成一个 VN 按钮预制体（选项 / 跳过 / 自动播放共用）。</summary>
        public static Button BuildButtonPrefab(string prefabName, Sprite plate, Sprite glow, Vector2 size, string assetPath)
        {
            // 中文标签需要中文字体资产，不存在时顺带生成。
            var font = ApplyChineseFontToVnTexts.LoadOrCreateFontAsset();

            var buttonObject = new GameObject(prefabName, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = buttonObject.GetComponent<RectTransform>();

            var hasBorder = plate != null && plate.border != Vector4.zero;

            var image = buttonObject.GetComponent<Image>();
            if (plate != null)
            {
                image.sprite = plate;
                image.type = hasBorder ? Image.Type.Sliced : Image.Type.Simple;
                // 没有九宫格边框就只能按原比例画，避免圆角/描边被拉伸。
                image.preserveAspect = !hasBorder;
            }
            else
            {
                image.color = new Color(0.118f, 0.125f, 0.161f, 0.92f);
            }

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            // 悬停/选中交给 VnChoiceButtonFx 的柔光，这里只给按下一点反馈，避免两套反馈打架。
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = Color.white,
                pressedColor = new Color(0.86f, 0.88f, 0.94f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.55f),
                colorMultiplier = 1f,
                fadeDuration = 0.06f
            };

            var layoutSize = hasBorder ? size : plate != null ? plate.rect.size : size;
            rect.sizeDelta = layoutSize;
            // 右上锚点：VnSceneUiView 运行时按 choiceAlign 重新定位。
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = Vector2.zero;

            var layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = layoutSize.x;
            layoutElement.preferredHeight = layoutSize.y;

            if (glow != null)
            {
                CreateGlowLayer(buttonObject, glow);
            }

            CreateLabelLayer(buttonObject, layoutSize, font);

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(buttonObject, assetPath);
            Object.DestroyImmediate(buttonObject);

            if (savedPrefab == null)
            {
                Debug.LogError($"[VN] 按钮预制体保存失败：{assetPath}");
                return null;
            }

            Debug.Log($"[VN] 已生成 {prefabName}：底板 {plate?.name ?? "(纯色)"}，尺寸 {layoutSize.x}×{layoutSize.y}" +
                      $"，{(hasBorder ? "九宫格" : "原比例固定尺寸")}{(glow != null ? " + 柔光层" : string.Empty)}，路径 {assetPath}");
            return savedPrefab.GetComponent<Button>();
        }

        /// <summary>柔光描边层：与底板同形，默认全透明，悬停时由 VnChoiceButtonFx 淡入。</summary>
        private static void CreateGlowLayer(GameObject buttonObject, Sprite glow)
        {
            var glowObject = new GameObject("Glow", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            glowObject.transform.SetParent(buttonObject.transform, false);

            var glowRect = glowObject.GetComponent<RectTransform>();
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;

            var glowImage = glowObject.GetComponent<Image>();
            glowImage.sprite = glow;
            glowImage.type = glow.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            glowImage.raycastTarget = false;

            var glowGroup = glowObject.GetComponent<CanvasGroup>();
            glowGroup.alpha = 0f;
            glowGroup.interactable = false;
            glowGroup.blocksRaycasts = false;

            var fx = buttonObject.AddComponent<VnChoiceButtonFx>();
            var fxSerialized = new SerializedObject(fx);
            fxSerialized.FindProperty("glowGroup").objectReferenceValue = glowGroup;
            fxSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>文字层：深色板上用白字，字号与内边距都按按钮尺寸推导。</summary>
        private static void CreateLabelLayer(GameObject buttonObject, Vector2 size, TMP_FontAsset font)
        {
            var padding = new Vector2(Mathf.Max(12f, size.x * 0.06f), Mathf.Max(4f, size.y * 0.12f));
            var fontSize = Mathf.Clamp(size.y * 0.42f, 14f, 40f);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);

            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = padding;
            labelRect.offsetMax = -padding;

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
            }

            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = string.Empty;
        }

        private static void WireScene(Button prefabButton)
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
            var rootProperty = viewSerialized.FindProperty("choiceRoot");
            var prefabProperty = viewSerialized.FindProperty("choiceButtonPrefab");
            var spriteProperty = viewSerialized.FindProperty("choiceButtonSprite");
            if (rootProperty == null || prefabProperty == null)
            {
                Debug.LogError("[VN] VnSceneUiView 上没有 choiceRoot / choiceButtonPrefab 字段，请先让 Unity 编译新脚本。");
                return;
            }

            var root = rootProperty.objectReferenceValue as RectTransform;
            if (root == null)
            {
                root = FindChoiceRoot(view);
                if (root == null)
                {
                    Debug.LogError($"[VN] 找不到选项容器 {ChoiceRootName}，请手动把它拖到 VnSceneUiView.choiceRoot。");
                    return;
                }

                rootProperty.objectReferenceValue = root;
            }

            prefabProperty.objectReferenceValue = prefabButton;
            if (spriteProperty != null)
            {
                // 代码兜底按钮也用同一张底板图，两条生成路径外观一致。
                spriteProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(ChoicePlatePath);
            }

            var removed = RemoveLegacyButtons(root);
            viewSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[VN] 已接好 choiceRoot / choiceButtonPrefab，删除旧的固定选项按钮 {removed} 个，并保存场景：{ScenePath}");
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

        private static RectTransform FindChoiceRoot(VnSceneUiView view)
        {
            var panel = new SerializedObject(view).FindProperty("vnPanel")?.objectReferenceValue as GameObject;
            return panel == null ? null : panel.transform.Find(ChoiceRootName) as RectTransform;
        }

        /// <summary>
        /// 删除旧版固定在场景里的选项按钮（只认 ChoiceButton1..N 这种"前缀 + 纯数字"的名字）；
        /// 生成式按钮是运行时创建的，名字不带数字，即使在 Play Mode 下也不会被误删。
        /// </summary>
        private static int RemoveLegacyButtons(RectTransform root)
        {
            var removed = 0;
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (!IsLegacyButtonName(child.name))
                {
                    continue;
                }

                Object.DestroyImmediate(child.gameObject);
                removed++;
            }

            return removed;
        }

        private static bool IsLegacyButtonName(string name)
        {
            if (!name.StartsWith(LegacyButtonPrefix, System.StringComparison.Ordinal))
            {
                return false;
            }

            var suffix = name.Substring(LegacyButtonPrefix.Length);
            if (suffix.Length == 0)
            {
                return false;
            }

            foreach (var c in suffix)
            {
                if (!char.IsDigit(c))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
