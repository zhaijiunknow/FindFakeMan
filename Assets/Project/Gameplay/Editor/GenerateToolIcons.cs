using System;
using System.IO;
using Project.Core.Runtime.Framework;
using Project.Gameplay.Scripts.Items;
using UnityEditor;
using UnityEngine;

namespace Project.Gameplay.Editor
{
    /// <summary>
    /// 用代码生成**物品图标**：工具（工具槽里那张小图）和线索（详情区图标位 + 收容格里那张小图）。
    ///
    /// 为什么是生成而不是等美术出图：这两类东西在玩法里都只是"手上/格子里的一个东西"，
    /// 先用一套风格统一、一眼能认出来的线稿顶上。
    /// 以后有真美术图，直接把同名 PNG 换成美术图就行 —— 资产的 <c>icon</c> 已经指着这个路径，不用改代码。
    ///
    /// 顺带填掉一个洞：线索资产的 icon 之前是空的，而收容格是"没有图标就不显示"
    /// （<c>InvestigationHudView.SetContainment</c>），所以收容了线索格子里其实是空的。
    ///
    /// 风格：128×128 透明底，描边 + 一圈同色柔光（和大软件 UI 的"深板 + 柔光"语言一致）。
    /// 工具用浅青白（跟 UI 同色系），线索用各自的主题色（水渍=紫外冷绿、头发=暖白、记录=纸色）以便区分。
    /// 形状用 SDF（到线段 / 圆 / 圆角矩形 / 圆弧的距离）算像素覆盖率，所以边是抗锯齿的，不是硬像素。
    ///
    /// 菜单：**Tools/Project/Gameplay/Generate Item Icons**（强制重画全部图标，并顺手改写资产的 icon）
    /// 建造工具调用的是 <see cref="EnsureIcon"/> / <see cref="EnsureClueIcon"/>：
    /// 只在文件不存在时生成，不会覆盖你手改过的图。
    /// </summary>
    public static class GenerateToolIcons
    {
        private const string IconFolder = "Assets/Project/Resource/UI/ItemIcons";
        private const string ToolFolder = "Assets/Project/Gameplay/ScriptableObjects/Tools";
        private const string ItemFolder = "Assets/Project/Gameplay/ScriptableObjects/Items";
        private const int Size = 128;

        /// <summary>要生成图标的线索资产名（和 ScriptableObjects/Items 下的文件名一致）。</summary>
        private static readonly string[] ClueAssetNames =
        {
            "Clue_WaterStain",
            "Clue_SofaHair",
            "Clue_DrawerRecord",
        };

        /// <summary>柔光宽度（比线宽多出来的那一圈）。</summary>
        private const float GlowWidth = 7f;

        private static readonly Color LineColor = new Color(0.82f, 0.93f, 1f, 1f);
        private static readonly Color DimColor = new Color(0.82f, 0.93f, 1f, 0.45f);
        private static readonly Color GlowColor = new Color(0.25f, 0.62f, 1f, 0.15f);
        private static readonly Color UvColor = new Color(0.74f, 0.56f, 1f, 1f);
        private static readonly Color WarmColor = new Color(0.98f, 0.62f, 0.42f, 1f);
        private static readonly Color RecordColor = new Color(0.95f, 0.42f, 0.42f, 1f);

        // 线索用各自的主题色，和工具的浅青白区分开。
        private static readonly Color StainColor = new Color(0.45f, 0.95f, 0.78f, 1f);
        private static readonly Color StainDimColor = new Color(0.45f, 0.95f, 0.78f, 0.4f);
        private static readonly Color HairColor = new Color(0.95f, 0.86f, 0.72f, 1f);
        private static readonly Color PaperColor = new Color(0.93f, 0.91f, 0.82f, 1f);
        private static readonly Color PaperDimColor = new Color(0.93f, 0.91f, 0.82f, 0.45f);

        [MenuItem("Tools/Project/Gameplay/Generate Item Icons")]
        public static void GenerateAll()
        {
            EnsureFolder();
            var count = 0;
            foreach (ToolType type in Enum.GetValues(typeof(ToolType)))
            {
                if (EnsureIcon(type, true) != null)
                {
                    count++;
                }
            }

            foreach (var clueName in ClueAssetNames)
            {
                if (EnsureClueIcon(clueName, true) != null)
                {
                    count++;
                }
            }

            // 顺手把已有物品资产的 icon 指到新图标：这样只跑这个菜单也能立刻看到效果（不必重建场景）。
            var rewired = RewireItemAssets();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ItemIcon] 已生成/重画 {count} 张图标 → {IconFolder}；顺手改了 {rewired} 个物品资产的 icon（没有的资产不新建）。");
        }

        /// <summary>把已存在的工具 / 线索资产的 icon 指向刚生成的图标（资产不存在就跳过）。</summary>
        private static int RewireItemAssets()
        {
            var count = 0;

            foreach (ToolType type in Enum.GetValues(typeof(ToolType)))
            {
                var assetName = AssetNameOf(type);
                var fileName = FileNameOf(type);
                if (assetName == null || fileName == null)
                {
                    continue;
                }

                if (AssignIcon($"{ToolFolder}/{assetName}.asset", $"{IconFolder}/{fileName}.png"))
                {
                    count++;
                }
            }

            foreach (var clueName in ClueAssetNames)
            {
                if (AssignIcon($"{ItemFolder}/{clueName}.asset", $"{IconFolder}/{ClueFileNameOf(clueName)}.png"))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool AssignIcon(string assetPath, string iconPath)
        {
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            var asset = AssetDatabase.LoadAssetAtPath<Item>(assetPath);
            if (icon == null || asset == null)
            {
                return false;
            }

            var so = new SerializedObject(asset);
            so.FindProperty("icon").objectReferenceValue = icon;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>工具资产文件名（和建造工具里的命名保持一致）。</summary>
        private static string AssetNameOf(ToolType type)
        {
            switch (type)
            {
                case ToolType.ToolKit: return "Tool_ToolKit";
                case ToolType.UVLight: return "Tool_UVLight";
                case ToolType.Detector: return "Tool_Detector";
                case ToolType.Thermometer: return "Tool_Thermometer";
                case ToolType.Recorder: return "Tool_Recorder";
                default: return null;
            }
        }

        /// <summary>取图标；没有就生成（不会覆盖已存在的图）。</summary>
        public static Sprite EnsureIcon(ToolType type)
        {
            return EnsureIcon(type, false);
        }

        private static Sprite EnsureIcon(ToolType type, bool forceRebuild)
        {
            var fileName = FileNameOf(type);
            if (fileName == null)
            {
                return null;
            }

            var path = $"{IconFolder}/{fileName}.png";
            if (!forceRebuild)
            {
                var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (existing != null)
                {
                    return existing;
                }
            }

            EnsureFolder();

            var pixels = NewCanvas();
            switch (type)
            {
                case ToolType.ToolKit:
                    DrawToolKit(pixels);
                    break;
                case ToolType.UVLight:
                    DrawUVLight(pixels);
                    break;
                case ToolType.Detector:
                    DrawDetector(pixels);
                    break;
                case ToolType.Thermometer:
                    DrawThermometer(pixels);
                    break;
                case ToolType.Recorder:
                    DrawRecorder(pixels);
                    break;
                default:
                    return null;
            }

            return WriteIcon(pixels, path);
        }

        private static string FileNameOf(ToolType type)
        {
            switch (type)
            {
                case ToolType.ToolKit: return "Icon_ToolKit";
                case ToolType.UVLight: return "Icon_UVLight";
                case ToolType.Detector: return "Icon_Detector";
                case ToolType.Thermometer: return "Icon_Thermometer";
                case ToolType.Recorder: return "Icon_Recorder";
                default: return null;
            }
        }

        /// <summary>取线索图标；没有就生成（不会覆盖已存在的图）。</summary>
        public static Sprite EnsureClueIcon(string clueAssetName)
        {
            return EnsureClueIcon(clueAssetName, false);
        }

        private static Sprite EnsureClueIcon(string clueAssetName, bool forceRebuild)
        {
            var fileName = ClueFileNameOf(clueAssetName);
            if (fileName == null)
            {
                return null;
            }

            var path = $"{IconFolder}/{fileName}.png";
            if (!forceRebuild)
            {
                var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (existing != null)
                {
                    return existing;
                }
            }

            EnsureFolder();

            var pixels = NewCanvas();
            switch (clueAssetName)
            {
                case "Clue_WaterStain":
                    DrawClueWaterStain(pixels);
                    break;
                case "Clue_SofaHair":
                    DrawClueSofaHair(pixels);
                    break;
                case "Clue_DrawerRecord":
                    DrawClueDrawerRecord(pixels);
                    break;
                default:
                    return null;
            }

            return WriteIcon(pixels, path);
        }

        private static string ClueFileNameOf(string clueAssetName)
        {
            switch (clueAssetName)
            {
                case "Clue_WaterStain": return "Icon_Clue_WaterStain";
                case "Clue_SofaHair": return "Icon_Clue_SofaHair";
                case "Clue_DrawerRecord": return "Icon_Clue_DrawerRecord";
                default: return null;
            }
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(IconFolder))
            {
                return;
            }

            AssetDatabase.CreateFolder("Assets/Project/Resource/UI", "ItemIcons");
        }

        private static Color32[] NewCanvas()
        {
            var pixels = new Color32[Size * Size];
            var clear = new Color32(255, 255, 255, 0);
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            return pixels;
        }

        // ---------- 五个图形（坐标 y 向上，画布 128×128，内容大致在 20..108）----------

        /// <summary>工具包：箱体 + 盖子缝 + 提手 + 锁扣。</summary>
        private static void DrawToolKit(Color32[] px)
        {
            StrokeRoundedRectGlow(px, new Rect(22f, 22f, 84f, 62f), 9f, 5f);
            StrokeSegmentGlow(px, new Vector2(24f, 70f), new Vector2(104f, 70f), 4f);
            StrokeArcGlow(px, new Vector2(64f, 84f), 15f, 15f, 165f, 5f);
            FillRoundedRect(px, new Rect(55f, 63f, 18f, 14f), 3f, LineColor);
        }

        /// <summary>紫外线灯：手柄 + 灯头 + 三道紫外射线。</summary>
        private static void DrawUVLight(Color32[] px)
        {
            StrokeRoundedRectGlow(px, new Rect(52f, 24f, 24f, 50f), 8f, 5f);
            StrokeRoundedRectGlow(px, new Rect(44f, 72f, 40f, 18f), 6f, 5f);
            StrokeSegment(px, new Vector2(52f, 95f), new Vector2(46f, 109f), 4f, UvColor);
            StrokeSegment(px, new Vector2(64f, 95f), new Vector2(64f, 111f), 4f, UvColor);
            StrokeSegment(px, new Vector2(76f, 95f), new Vector2(82f, 109f), 4f, UvColor);
        }

        /// <summary>便携探测器：机身 + 屏幕 + 三根电平条 + 天线（刻意不做成雷达盘）。</summary>
        private static void DrawDetector(Color32[] px)
        {
            StrokeRoundedRectGlow(px, new Rect(28f, 22f, 72f, 62f), 9f, 5f);
            StrokeRoundedRect(px, new Rect(38f, 44f, 52f, 32f), 4f, 3f, DimColor);

            // 电平条：左低右高，一眼看出是"读数"。
            FillRoundedRect(px, new Rect(46f, 50f, 8f, 8f), 2f, DimColor);
            FillRoundedRect(px, new Rect(58f, 50f, 8f, 16f), 2f, LineColor);
            FillRoundedRect(px, new Rect(70f, 50f, 8f, 24f), 2f, LineColor);

            StrokeSegmentGlow(px, new Vector2(88f, 84f), new Vector2(101f, 105f), 4f);
            FillCircle(px, new Vector2(101f, 105f), 6f, LineColor);
        }

        /// <summary>温度计：玻璃管 + 球 + 水银柱 + 刻度。</summary>
        private static void DrawThermometer(Color32[] px)
        {
            StrokeRoundedRectGlow(px, new Rect(56f, 38f, 16f, 68f), 8f, 4.5f);
            StrokeCircleGlow(px, new Vector2(64f, 34f), 15f, 4.5f);
            FillRoundedRect(px, new Rect(61f, 38f, 6f, 48f), 3f, WarmColor);
            FillCircle(px, new Vector2(64f, 34f), 10f, WarmColor);

            for (var i = 0; i < 3; i++)
            {
                var y = 62f + i * 14f;
                StrokeSegment(px, new Vector2(76f, y), new Vector2(84f, y), 3.5f, DimColor);
            }
        }

        /// <summary>录音设备：机身 + 麦克风 + 喇叭孔 + 录制红点。</summary>
        private static void DrawRecorder(Color32[] px)
        {
            StrokeRoundedRectGlow(px, new Rect(22f, 34f, 84f, 60f), 10f, 5f);
            StrokeCircleGlow(px, new Vector2(48f, 64f), 14f, 4.5f);
            FillCircle(px, new Vector2(48f, 64f), 5f, LineColor);

            for (var row = 0; row < 2; row++)
            {
                for (var col = 0; col < 3; col++)
                {
                    FillCircle(px, new Vector2(72f + col * 11f, 76f - row * 22f), 4f, DimColor);
                }
            }

            FillCircle(px, new Vector2(34f, 84f), 4.5f, RecordColor);
        }

        // ---------- 三个线索图形（各用主题色，和工具的浅青白区分开）----------

        /// <summary>荧光水渍：一滩不规则的渍 + 内圈 + 两滴飞溅（紫外线下那种冷绿）。</summary>
        private static void DrawClueWaterStain(Color32[] px)
        {
            StrokeClosedPolyline(px, BlobPoints(new Vector2(58f, 54f), 30f, 0.16f, 0.6f), 4.5f, StainColor);
            StrokeClosedPolyline(px, BlobPoints(new Vector2(58f, 54f), 18f, 0.22f, 2.1f), 3f, StainDimColor);

            // 飞溅：一小滴 + 一道短痕（"擦过/淌过"的痕迹）
            StrokeCircleGlow(px, new Vector2(100f, 40f), 6f, 3.5f, StainColor);
            StrokeSegmentGlow(px, new Vector2(88f, 84f), new Vector2(98f, 92f), 3.5f, StainDimColor);
        }

        /// <summary>坐垫夹层的头发：三缕不同心的弧 + 两端翘出去的碎发。</summary>
        private static void DrawClueSofaHair(Color32[] px)
        {
            StrokeArcGlow(px, new Vector2(58f, 24f), 54f, 28f, 152f, 4.5f, HairColor);
            StrokeArcGlow(px, new Vector2(68f, 34f), 44f, 22f, 158f, 4f, HairColor);
            StrokeArcGlow(px, new Vector2(64f, 16f), 62f, 38f, 142f, 3.5f, HairColor);

            // 碎发：从主弧两端翘出去的两小段（不然三缕弧看着像信号图标，不像头发）
            StrokeSegmentGlow(px, new Vector2(30f, 52f), new Vector2(18f, 64f), 3f, HairColor);
            StrokeSegmentGlow(px, new Vector2(106f, 52f), new Vector2(118f, 66f), 3f, HairColor);
        }

        /// <summary>抽屉里的记录：一页纸 + 几行字，其中一行开始"变形"（对得上设计里"字迹突然变形"）。</summary>
        private static void DrawClueDrawerRecord(Color32[] px)
        {
            StrokeRoundedRectGlow(px, new Rect(34f, 24f, 60f, 78f), 6f, 4.5f, PaperColor);

            StrokeSegment(px, new Vector2(44f, 84f), new Vector2(84f, 84f), 3.5f, PaperDimColor);
            StrokeSegment(px, new Vector2(44f, 73f), new Vector2(84f, 73f), 3.5f, PaperDimColor);
            StrokeSegment(px, new Vector2(44f, 62f), new Vector2(70f, 62f), 3.5f, PaperDimColor);

            // 第四行扭掉：三段不在一条线上的笔画，中间那段泛着冷光（异常的那一笔）
            StrokeSegment(px, new Vector2(44f, 49f), new Vector2(56f, 53f), 3.5f, PaperDimColor);
            StrokeSegment(px, new Vector2(56f, 53f), new Vector2(68f, 46f), 3.5f, StainColor);
            StrokeSegment(px, new Vector2(68f, 46f), new Vector2(84f, 50f), 3.5f, PaperDimColor);

            StrokeSegment(px, new Vector2(44f, 35f), new Vector2(62f, 35f), 3.5f, PaperDimColor);
        }

        // ---------- 描边 / 填充（SDF + 覆盖率抗锯齿）----------

        /// <summary>描边 = 先画一圈淡蓝柔光，再压上青白实线（工具图标用的那套）。</summary>
        private static void StrokeGlow(Color32[] px, Rect bounds, float width, Func<Vector2, float> distance)
        {
            var pad = width * 0.5f + GlowWidth + 2f;
            StrokeShape(px, bounds, pad, width + GlowWidth, GlowColor, distance);
            StrokeShape(px, bounds, pad, width, LineColor, distance);
        }

        /// <summary>描边 + **同色**柔光（线索图标用主题色时走这个）。</summary>
        private static void StrokeGlow(Color32[] px, Rect bounds, float width, Func<Vector2, float> distance, Color color)
        {
            var pad = width * 0.5f + GlowWidth + 2f;
            var glow = new Color(color.r, color.g, color.b, 0.2f);
            StrokeShape(px, bounds, pad, width + GlowWidth, glow, distance);
            StrokeShape(px, bounds, pad, width, color, distance);
        }

        /// <summary>不发光的一笔（用于刻度、字迹这类细节）。</summary>
        private static void StrokeSegment(Color32[] px, Vector2 a, Vector2 b, float width, Color color)
        {
            StrokeShape(px, BoundsOf(a, b), width * 0.5f + 2f, width, color, p => DistanceToSegment(p, a, b));
        }

        private static void StrokeSegmentGlow(Color32[] px, Vector2 a, Vector2 b, float width)
        {
            StrokeGlow(px, BoundsOf(a, b), width, p => DistanceToSegment(p, a, b));
        }

        private static void StrokeSegmentGlow(Color32[] px, Vector2 a, Vector2 b, float width, Color color)
        {
            StrokeGlow(px, BoundsOf(a, b), width, p => DistanceToSegment(p, a, b), color);
        }

        private static void StrokeCircleGlow(Color32[] px, Vector2 center, float radius, float width)
        {
            StrokeGlow(px, BoundsOfCircle(center, radius), width, p => Mathf.Abs((p - center).magnitude - radius));
        }

        private static void StrokeCircleGlow(Color32[] px, Vector2 center, float radius, float width, Color color)
        {
            StrokeGlow(px, BoundsOfCircle(center, radius), width, p => Mathf.Abs((p - center).magnitude - radius), color);
        }

        private static void StrokeArcGlow(Color32[] px, Vector2 center, float radius, float fromDeg, float toDeg, float width)
        {
            StrokeGlow(px, BoundsOfCircle(center, radius), width, p => DistanceToArc(p, center, radius, fromDeg, toDeg));
        }

        private static void StrokeArcGlow(Color32[] px, Vector2 center, float radius, float fromDeg, float toDeg, float width, Color color)
        {
            StrokeGlow(px, BoundsOfCircle(center, radius), width, p => DistanceToArc(p, center, radius, fromDeg, toDeg), color);
        }

        private static void StrokeRoundedRectGlow(Color32[] px, Rect rect, float radius, float width)
        {
            StrokeGlow(px, rect, width, p => Mathf.Abs(RoundedRectDistance(p, rect, radius)));
        }

        private static void StrokeRoundedRectGlow(Color32[] px, Rect rect, float radius, float width, Color color)
        {
            StrokeGlow(px, rect, width, p => Mathf.Abs(RoundedRectDistance(p, rect, radius)), color);
        }

        /// <summary>把一串点依次连成闭环（够密就看不出是一段段的）：用来画不规则的水渍轮廓。</summary>
        private static void StrokeClosedPolyline(Color32[] px, Vector2[] points, float width, Color color)
        {
            for (var i = 0; i < points.Length; i++)
            {
                StrokeSegmentGlow(px, points[i], points[(i + 1) % points.Length], width, color);
            }
        }

        /// <summary>不规则闭合轮廓：半径随角度做两组正弦摆动，出来就是"不是圆"的自然形状。</summary>
        private static Vector2[] BlobPoints(Vector2 center, float radius, float wobble, float phase)
        {
            const int count = 28;
            var points = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                var t = (float)i / count * Mathf.PI * 2f;
                var wave = Mathf.Sin(t * 2f + phase) * 0.6f + Mathf.Sin(t * 3f - phase * 1.7f) * 0.4f;
                var r = radius * (1f + wobble * wave);
                points[i] = center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r;
            }

            return points;
        }

        private static void StrokeRoundedRect(Color32[] px, Rect rect, float radius, float width, Color color)
        {
            StrokeShape(px, rect, width * 0.5f + 2f, width, color, p => Mathf.Abs(RoundedRectDistance(p, rect, radius)));
        }

        private static void FillRoundedRect(Color32[] px, Rect rect, float radius, Color color)
        {
            FillShape(px, rect, color, p => RoundedRectDistance(p, rect, radius));
        }

        private static void FillCircle(Color32[] px, Vector2 center, float radius, Color color)
        {
            FillShape(px, BoundsOfCircle(center, radius), color, p => (p - center).magnitude - radius);
        }

        private static void StrokeShape(Color32[] px, Rect bounds, float pad, float width, Color color, Func<Vector2, float> distance)
        {
            var half = width * 0.5f;
            foreach (var i in Iterate(bounds, pad))
            {
                var p = new Vector2(i % Size, i / Size);
                // 描边：|到中心线的距离| 越小越实，半宽边界处 1 像素过渡。
                var edge = Mathf.Clamp01(half + 0.5f - distance(p));
                Paint(px, i, color, color.a * edge);
            }
        }

        private static void FillShape(Color32[] px, Rect bounds, Color color, Func<Vector2, float> sdf)
        {
            foreach (var i in Iterate(bounds, 2f))
            {
                var p = new Vector2(i % Size, i / Size);
                // 填充：sdf < 0 在形状内部，边界外 0.5 像素内渐变 = 1 像素抗锯齿。
                var coverage = Mathf.Clamp01(0.5f - sdf(p));
                Paint(px, i, color, color.a * coverage);
            }
        }

        /// <summary>遍历包围盒内的像素下标（自动裁到画布内）。</summary>
        private static System.Collections.Generic.IEnumerable<int> Iterate(Rect bounds, float pad)
        {
            var xMin = Mathf.Max(0, Mathf.FloorToInt(bounds.xMin - pad));
            var xMax = Mathf.Min(Size - 1, Mathf.CeilToInt(bounds.xMax + pad));
            var yMin = Mathf.Max(0, Mathf.FloorToInt(bounds.yMin - pad));
            var yMax = Mathf.Min(Size - 1, Mathf.CeilToInt(bounds.yMax + pad));
            for (var y = yMin; y <= yMax; y++)
            {
                for (var x = xMin; x <= xMax; x++)
                {
                    yield return y * Size + x;
                }
            }
        }

        /// <summary>把一笔颜色按 alpha 叠到画布上（普通 over 混合）。</summary>
        private static void Paint(Color32[] px, int index, Color color, float alpha)
        {
            if (index < 0 || index >= px.Length)
            {
                return;
            }

            var srcA = Mathf.Clamp01(alpha);
            if (srcA <= 0.0001f)
            {
                return;
            }

            var dst = px[index];
            var dstA = dst.a / 255f;
            var outA = srcA + dstA * (1f - srcA);
            if (outA <= 0.0001f)
            {
                px[index] = new Color32(0, 0, 0, 0);
                return;
            }

            var r = (color.r * srcA + dst.r / 255f * dstA * (1f - srcA)) / outA;
            var g = (color.g * srcA + dst.g / 255f * dstA * (1f - srcA)) / outA;
            var b = (color.b * srcA + dst.b / 255f * dstA * (1f - srcA)) / outA;
            px[index] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(b * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(outA * 255f), 0, 255));
        }

        // ---------- 距离函数 ----------

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSq = ab.sqrMagnitude;
            if (lengthSq <= 0.0001f)
            {
                return (p - a).magnitude;
            }

            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
            return (p - (a + ab * t)).magnitude;
        }

        private static float DistanceToArc(Vector2 p, Vector2 center, float radius, float fromDeg, float toDeg)
        {
            var v = p - center;
            var angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }

            var from = Normalize(fromDeg);
            var to = Normalize(toDeg);
            var inRange = from <= to ? angle >= from && angle <= to : angle >= from || angle <= to;
            if (inRange)
            {
                return Mathf.Abs(v.magnitude - radius);
            }

            var a = center + new Vector2(Mathf.Cos(from * Mathf.Deg2Rad), Mathf.Sin(from * Mathf.Deg2Rad)) * radius;
            var b = center + new Vector2(Mathf.Cos(to * Mathf.Deg2Rad), Mathf.Sin(to * Mathf.Deg2Rad)) * radius;
            return Mathf.Min((p - a).magnitude, (p - b).magnitude);
        }

        private static float Normalize(float degrees)
        {
            var d = degrees % 360f;
            return d < 0f ? d + 360f : d;
        }

        /// <summary>圆角矩形的有符号距离（内部为负）。</summary>
        private static float RoundedRectDistance(Vector2 p, Rect rect, float radius)
        {
            var half = new Vector2(rect.width, rect.height) * 0.5f - Vector2.one * radius;
            var d = new Vector2(Mathf.Abs(p.x - rect.center.x) - half.x, Mathf.Abs(p.y - rect.center.y) - half.y);
            var outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude;
            var inside = Mathf.Min(Mathf.Max(d.x, d.y), 0f);
            return outside + inside - radius;
        }

        private static Rect BoundsOf(Vector2 a, Vector2 b)
        {
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private static Rect BoundsOfCircle(Vector2 center, float radius)
        {
            return new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
        }

        // ---------- 写盘 ----------

        private static Sprite WriteIcon(Color32[] pixels, string assetPath)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();

            var bytes = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);

            File.WriteAllBytes(assetPath, bytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 256;
                // 细线 + 透明底：压缩会把边糊掉，所以图标不压缩（128×128 每张才 64 KB）。
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            Debug.Log($"[ItemIcon] 生成图标：{assetPath}（{(sprite != null ? "OK" : "加载失败")}）");
            return sprite;
        }
    }
}
