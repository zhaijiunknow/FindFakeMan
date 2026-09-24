using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Project.Gameplay.Editor
{
    /// <summary>
    /// 修美术导入设置：`别墅客厅` 下的贴图被导成了 Multiple(spriteMode 2) 但 spriteSheet 是空的，
    /// 结果是这些贴图**根本没有 Sprite 子资源**，Image 上根本拖不进去，玩法场景用不了。
    ///
    /// 这个工具把它们改回 Single，并强制重导。以后往这个文件夹里丢新图也不会再踩同一个坑
    /// （<see cref="LivingRoomSpritePostprocessor"/> 会在导入时自动纠正）。
    ///
    /// 另外补上玩法场景"图自己就是按钮"需要的两项：
    /// - **Read/Write = true**：`Image.alphaHitTestMinimumThreshold` 的前提。但可读贴图会多一份内存
    ///   （3840×2160 RGBA32 ≈ 33 MB/张），所以**只给真正用到的图开**（见 <see cref="ReadablePrefixes"/>，
    ///   玩法场景用的是夜版 `3b`；白天的 `3a` 变体不需要，之前被开过的会被关回去省内存）。
    /// - **Mesh Type = Full Rect**：alpha 命中的另一个前提。这一项不花内存，所以整文件夹统一设。
    ///
    /// 菜单：Tools/Project/Gameplay/Fix Living Room Sprite Import
    /// 幂等：设置已经对的不动，也不碰 spriteBorder（VN 的九宫格图不受影响）。
    /// </summary>
    public static class FixLivingRoomSpriteImport
    {
        /// <summary>需要保证是 Single 的贴图文件夹。</summary>
        internal static readonly string[] Folders =
        {
            "Assets/Project/Resource/别墅客厅",
        };

        /// <summary>
        /// 需要开 Read/Write 的文件名前缀。玩法场景只用夜版 `3b`，
        /// 所以白天的 `3a` 变体保持不可读（每张省约 33 MB）。
        /// </summary>
        internal static readonly string[] ReadablePrefixes =
        {
            "3b",
        };

        /// <summary>
        /// 例外：这些图**不**需要 Read/Write。
        /// - 背景是纯静态底图（`raycastTarget = false`）；
        /// - 抽屉特写是整帧**不透明**图，当覆盖层用（矩形判定就等于 alpha 判定）。
        /// 它们都不做 alpha 命中，所以不必各多花 33 MB。
        /// </summary>
        internal static readonly string[] NotReadableNames =
        {
            "3b背景.png",
            "3b抽屉特写.png",
        };

        /// <summary>这张贴图是否需要 Read/Write（只有"图自己就是按钮"的家具图层需要）。</summary>
        internal static bool NeedsReadable(string assetPath)
        {
            var fileName = System.IO.Path.GetFileName(assetPath);

            foreach (var name in NotReadableNames)
            {
                if (string.Equals(fileName, name, System.StringComparison.Ordinal))
                {
                    return false;
                }
            }

            foreach (var prefix in ReadablePrefixes)
            {
                if (fileName.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>这张贴图的 Mesh Type 是不是 Full Rect。</summary>
        internal static bool IsFullRect(TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            return settings.spriteMeshType == SpriteMeshType.FullRect;
        }

        /// <summary>
        /// 把 Mesh Type 设成 Full Rect。返回是否真的改了。
        /// 注意 `TextureImporter` 上**没有** `spriteMeshType`（它在 `TextureImporterSettings` 里），
        /// 所以必须 ReadTextureSettings → 改 → SetTextureSettings；
        /// 这样是"读出来再改一项"，其它导入设置（九宫格 border、PPU 等）都原样保留。
        /// </summary>
        internal static bool EnsureFullRect(TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType == SpriteMeshType.FullRect)
            {
                return false;
            }

            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            return true;
        }

        [MenuItem("Tools/Project/Gameplay/Fix Living Room Sprite Import")]
        public static void Run()
        {
            var fixedCount = 0;
            var alreadyOk = 0;
            var skipped = new List<string>();

            foreach (var path in EnumerateTextures())
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    skipped.Add(path);
                    continue;
                }

                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                }

                var wantReadable = NeedsReadable(path);

                // 已经是对的：Single + 想要的 MeshType + Read/Write 与需求一致。
                // 注意这里也负责**关掉**多余的 Read/Write（历史上被全量开过的话，这里会把内存还回来）。
                if (importer.spriteImportMode == SpriteImportMode.Single &&
                    IsFullRect(importer) &&
                    importer.isReadable == wantReadable)
                {
                    alreadyOk++;
                    continue;
                }

                importer.spriteImportMode = SpriteImportMode.Single;

                // 这两项不花内存，整文件夹统一设。
                EnsureFullRect(importer);
                // Single 模式下要保证有个正常的中心锚点。
                if (importer.spritePivot == Vector2.zero)
                {
                    importer.spritePivot = new Vector2(0.5f, 0.5f);
                }

                importer.isReadable = wantReadable;

                importer.SaveAndReimport();
                fixedCount++;
                Debug.Log($"[FixSprites] 已修：{path}（Single + FullRect，Read/Write={wantReadable}）");
            }

            foreach (var path in skipped)
            {
                Debug.LogWarning($"[FixSprites] 不是 TextureImporter，跳过：{path}");
            }

            AssetDatabase.Refresh();
            Debug.Log($"[FixSprites] 完成：改好 {fixedCount} 张，本来就对的 {alreadyOk} 张。"
                      + $"（Read/Write 只开给 {string.Join("/", ReadablePrefixes)} 开头的图）");
        }

        internal static IEnumerable<string> EnumerateTextures()
        {
            foreach (var folder in Folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.LogWarning($"[FixSprites] 文件夹不存在，跳过：{folder}");
                    continue;
                }

                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    yield return AssetDatabase.GUIDToAssetPath(guid);
                }
            }
        }
    }

    /// <summary>保证指定文件夹里的贴图永远是 Single + FullRect，避免再出现"没有 Sprite 子资源"的图和 alpha 命中失效。</summary>
    public sealed class LivingRoomSpritePostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            var isTarget = false;
            foreach (var folder in FixLivingRoomSpriteImport.Folders)
            {
                if (assetPath.StartsWith(folder + "/", System.StringComparison.Ordinal))
                {
                    isTarget = true;
                    break;
                }
            }

            if (!isTarget)
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                Debug.Log($"[FixSprites] 导入时自动改为 Single：{assetPath}");
            }

            if (FixLivingRoomSpriteImport.EnsureFullRect(importer))
            {
                Debug.Log($"[FixSprites] 导入时自动改为 Full Rect：{assetPath}");
            }

            // alpha 命中需要可读贴图，但可读 = 多一份内存，所以只给玩法场景用到的图开。
            var wantReadable = FixLivingRoomSpriteImport.NeedsReadable(assetPath);
            if (importer.isReadable != wantReadable)
            {
                importer.isReadable = wantReadable;
                Debug.Log($"[FixSprites] 导入时自动设置 Read/Write={wantReadable}：{assetPath}");
            }
        }
    }
}
