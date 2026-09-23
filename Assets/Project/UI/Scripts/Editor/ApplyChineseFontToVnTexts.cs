using System.IO;
using Project.UI.Scripts;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace Project.UI.Editor
{
    /// <summary>
    /// 中文 TMP 字体工具：用项目自带的 ChillRoundF.ttf 生成动态字体资产，
    /// 并把它挂到 Px2050_Prologue 场景里 VnSceneUiView 的对白/说话人文本上。
    ///
    /// 菜单：Tools/Project/UI/Apply Chinese Font To VN Scene
    ///
    /// 说明：
    /// - 不修改 TMP Settings 的全局 fallback，也不动其它 Prefab / 场景。
    /// - 选项按钮的文字不在这个场景里：它们由 VnSceneUiView 生成，
    ///   预制体路径见 SetupVnChoiceButtons；生成时也会按需创建同一个字体资产。
    /// - 动态图集模式下缺字会在运行时按需光栅化，不需要预生成字符集；
    ///   若以后要给别的界面也用中文字体，直接把生成的字体资产拖到对应 TMP 文本即可。
    /// </summary>
    public static class ApplyChineseFontToVnTexts
    {
        /// <summary>中文字体源文件（导入设置里需勾选 Include Font Data）。</summary>
        public const string SourceFontPath =
            "Assets/ThirdParty/TextMesh Pro/Resources/Fonts & Materials/ChillRoundF.ttf";

        /// <summary>生成的 TMP 字体资产所在目录。</summary>
        public const string FontAssetFolder = "Assets/Project/Resource/Fonts";

        /// <summary>生成的 TMP 字体资产路径。</summary>
        public const string FontAssetPath = FontAssetFolder + "/ChillRoundF SDF.asset";

        private const string ScenePath = "Assets/Project/UI/Scenes/Px2050_Prologue.unity";

        // 动态图集参数：64pt 采样 + 8px padding，1024x1024 图集约可容纳 200 个汉字，放不下会自动开多张图集。
        private const int SamplingPointSize = 64;
        private const int AtlasPadding = 8;
        private const int AtlasWidth = 1024;
        private const int AtlasHeight = 1024;

        [MenuItem("Tools/Project/UI/Apply Chinese Font To VN Scene")]
        public static void Apply()
        {
            var fontAsset = LoadOrCreateFontAsset();
            if (fontAsset == null)
            {
                return;
            }

            ApplyToScene(fontAsset);
        }

        /// <summary>加载已生成的中文字体资产；不存在时从 TTF 生成（含 atlas 贴图与材质子资产）。</summary>
        public static TMP_FontAsset LoadOrCreateFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                return existing;
            }

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[Font] 未找到中文字体源文件：{SourceFontPath}");
                return null;
            }

            EnsureFolder();

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                SamplingPointSize,
                AtlasPadding,
                GlyphRenderMode.SDFAA,
                AtlasWidth,
                AtlasHeight,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                Debug.LogError($"[Font] 生成字体资产失败，请确认 {SourceFontPath} 的导入设置勾选了 Include Font Data。");
                return null;
            }

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            // atlas 贴图与材质必须作为子资产存进同一个 .asset，
            // 否则场景/预制体对材质的引用在重新打开工程后会变成 missing。
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Font] 已生成中文字体资产：{FontAssetPath}");
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(FontAssetFolder))
            {
                return;
            }

            var projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot != null)
            {
                Directory.CreateDirectory(Path.Combine(projectRoot.FullName, FontAssetFolder));
                AssetDatabase.Refresh();
            }
        }

        private static void ApplyToScene(TMP_FontAsset fontAsset)
        {
            // 场景已经打开就直接改，不打断当前编辑；没打开才需要切场景。
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.Log("[Font] 已取消：当前场景有未保存修改，操作中止。");
                    return;
                }

                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            if (!scene.IsValid())
            {
                Debug.LogError($"[Font] 无法打开场景：{ScenePath}");
                return;
            }

            var view = FindViewInScene(scene);
            if (view == null)
            {
                Debug.LogError($"[Font] 场景里没有 VnSceneUiView：{ScenePath}");
                return;
            }

            var viewSerialized = new SerializedObject(view);
            var applied = 0;
            applied += ApplyToText(viewSerialized.FindProperty("vnSpeakerText"), fontAsset);
            applied += ApplyToText(viewSerialized.FindProperty("vnBodyText"), fontAsset);
            // 选项按钮的文字来自生成式按钮：预制体（Setup VN Choice Buttons 生成时已挂中文字体）
            // 或运行时兜底按钮（继承 vnBodyText.font），所以这里不需要再单独处理。

            if (applied == 0)
            {
                Debug.LogWarning("[Font] 没有可应用的 TMP 文本，场景未修改。");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Font] 已把 {fontAsset.name} 应用到 {applied} 个 TMP 文本，并保存场景：{ScenePath}");
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

        private static int ApplyToText(SerializedProperty textProperty, TMP_FontAsset fontAsset)
        {
            var text = textProperty?.objectReferenceValue as TMP_Text;
            if (text == null)
            {
                return 0;
            }

            var textSerialized = new SerializedObject(text);
            var fontProperty = textSerialized.FindProperty("m_fontAsset");
            var materialProperty = textSerialized.FindProperty("m_sharedMaterial");
            if (fontProperty == null || materialProperty == null)
            {
                Debug.LogWarning($"[Font] {text.name} 上找不到 TMP 字体字段，已跳过。");
                return 0;
            }

            fontProperty.objectReferenceValue = fontAsset;
            // 同时写入材质，保证 fontAsset 与 material 配对，避免运行时回落到默认材质。
            materialProperty.objectReferenceValue = fontAsset.material;
            textSerialized.FindProperty("m_fontSharedMaterials")?.ClearArray();
            textSerialized.FindProperty("m_fontMaterials")?.ClearArray();

            // 先置 m_hasFontAssetChanged：ApplyModifiedProperties 会触发 TextMeshProUGUI.OnValidate，
            // 它会调用 LoadFontAsset() 让编辑器里已打开的文本当场换成新字体。
            var hasFontAssetChanged = textSerialized.FindProperty("m_hasFontAssetChanged");
            if (hasFontAssetChanged != null)
            {
                hasFontAssetChanged.boolValue = true;
            }

            textSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (hasFontAssetChanged != null)
            {
                // 字体已装载完成，把标记复位，避免场景每次打开都因为这个标记被标脏。
                textSerialized.Update();
                textSerialized.FindProperty("m_hasFontAssetChanged").boolValue = false;
                textSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(text);
            return 1;
        }
    }
}
