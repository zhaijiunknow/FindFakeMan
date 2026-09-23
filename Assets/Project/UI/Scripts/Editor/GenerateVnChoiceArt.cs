using System.IO;
using UnityEditor;
using UnityEngine;

namespace Project.UI.Editor
{
    /// <summary>
    /// 按项目既有的 UI 语言程序化生成 VN 选项按钮底板：
    /// 深色圆角板（同 大软件/item_noselect 的 #1E2029）+ 细柔光边；
    /// 另出一张同形状的"选中柔光"描边贴图（同 小软件/select_button 的蓝色描边语义）。
    ///
    /// 菜单：Tools/Project/UI/Generate VN Choice Button Art
    ///
    /// 说明：
    /// - 生成的是【九宫格】贴图（spriteBorder 已设），所以按钮可以拉宽不变形；
    /// - 贴图高度 == 按钮高度（48），纵向不拉伸，只有中间列会横向平铺；
    /// - 这两张是程序生成的占位美术，美术出图后直接覆盖同名 PNG 即可，代码/预制体不用改。
    /// </summary>
    public static class GenerateVnChoiceArt
    {
        /// <summary>生成的底板贴图路径（SetupVnChoiceButtons 会读它）。</summary>
        public const string PlatePath = "Assets/Project/Resource/UI/VN/VNChoicePlate.png";

        /// <summary>生成的选中柔光贴图路径。</summary>
        public const string GlowPath = "Assets/Project/Resource/UI/VN/VNChoiceGlow.png";

        // 与按钮高度一致，纵向不做九宫格拉伸。
        private const int PlateWidth = 128;
        private const int PlateHeight = 48;

        private const float CornerRadius = 13f;
        private const int SliceBorder = 18;

        private const float EdgeWidth = 2f;
        private const float GlowRimHalf = 1.6f;
        private const float GlowFalloff = 7f;
        private const float GlowInnerFalloff = 3f;

        /// <summary>板底色，取自 大软件/item_noselect.png。</summary>
        private static readonly Color FillColor = new Color(0.118f, 0.125f, 0.161f, 1f);

        /// <summary>细边颜色，带一点冷紫，和 item_noselect 的柔光边同调。</summary>
        private static readonly Color EdgeColor = new Color(0.72f, 0.74f, 0.87f, 0.55f);

        /// <summary>选中柔光颜色，取自 小软件/select_button.png 的蓝色描边。</summary>
        private static readonly Color GlowColor = new Color(0.29f, 0.55f, 1f, 1f);

        [MenuItem("Tools/Project/UI/Generate VN Choice Button Art")]
        public static void Generate()
        {
            WriteTexture(PlatePath, BuildPlate());
            WriteTexture(GlowPath, BuildGlow());
            AssetDatabase.Refresh();
            Debug.Log($"[VN] 已生成选项按钮底板与柔光贴图：{PlatePath} / {GlowPath}（{PlateWidth}×{PlateHeight}，九宫格 {SliceBorder}）");
        }

        /// <summary>贴图不存在时按需生成（Setup/Builder 会先调它）。</summary>
        public static void GenerateIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(PlatePath) != null &&
                AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath) != null)
            {
                return;
            }

            Generate();
        }

        private static Texture2D BuildPlate()
        {
            var pixels = new Color32[PlateWidth * PlateHeight];
            var halfW = PlateWidth * 0.5f;
            var halfH = PlateHeight * 0.5f;

            for (var y = 0; y < PlateHeight; y++)
            {
                for (var x = 0; x < PlateWidth; x++)
                {
                    var px = x + 0.5f - halfW;
                    var py = y + 0.5f - halfH;
                    var d = SdRoundedBox(px, py, halfW, halfH, CornerRadius);

                    var color = FillColor;
                    // 靠近边缘的一圈描细边，做出 item_noselect 那种"薄壳"观感。
                    if (d > -EdgeWidth)
                    {
                        color = Blend(EdgeColor, FillColor, Mathf.InverseLerp(-EdgeWidth, 0f, d));
                    }

                    var alpha = Mathf.Clamp01(0.5f - d); // 1px 抗锯齿
                    pixels[y * PlateWidth + x] = ToColor32(color, color.a * alpha);
                }
            }

            return CreateTexture(pixels);
        }

        private static Texture2D BuildGlow()
        {
            var pixels = new Color32[PlateWidth * PlateHeight];
            var halfW = PlateWidth * 0.5f;
            var halfH = PlateHeight * 0.5f;

            for (var y = 0; y < PlateHeight; y++)
            {
                for (var x = 0; x < PlateWidth; x++)
                {
                    var px = x + 0.5f - halfW;
                    var py = y + 0.5f - halfH;
                    var d = SdRoundedBox(px, py, halfW, halfH, CornerRadius);

                    float alpha;
                    if (d >= 0f)
                    {
                        // 外侧柔光：从描边向外衰减。
                        alpha = 1f - Mathf.Clamp01((d - GlowRimHalf) / GlowFalloff);
                    }
                    else
                    {
                        // 内侧也留一点过渡，避免描边看起来像贴上去的。
                        var inward = -d;
                        alpha = inward <= GlowRimHalf
                            ? 1f
                            : 1f - Mathf.Clamp01((inward - GlowRimHalf) / GlowInnerFalloff);
                    }

                    pixels[y * PlateWidth + x] = ToColor32(GlowColor, GlowColor.a * Mathf.Clamp01(alpha));
                }
            }

            return CreateTexture(pixels);
        }

        private static Texture2D CreateTexture(Color32[] pixels)
        {
            var texture = new Texture2D(PlateWidth, PlateHeight, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static void WriteTexture(string assetPath, Texture2D texture)
        {
            var bytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            var absolutePath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? Application.dataPath);
            File.WriteAllBytes(absolutePath, bytes);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[VN] 无法设置贴图导入参数：{assetPath}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(SliceBorder, SliceBorder, SliceBorder, SliceBorder);
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        /// <summary>圆角矩形的有符号距离场：&lt;0 在内部，&gt;0 在外部，单位是像素。</summary>
        private static float SdRoundedBox(float px, float py, float halfW, float halfH, float radius)
        {
            var qx = Mathf.Abs(px) - (halfW - radius);
            var qy = Mathf.Abs(py) - (halfH - radius);
            var ax = Mathf.Max(qx, 0f);
            var ay = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static Color Blend(Color top, Color bottom, float t)
        {
            return Color.Lerp(bottom, top, t);
        }

        private static Color32 ToColor32(Color color, float alpha)
        {
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
        }
    }
}
