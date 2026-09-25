using System;
using System.Collections.Generic;
using Project.Core.Runtime.Framework;
using UnityEngine;
// **别名必须起** ✗→✓：Unity 自己也有 `UnityEngine.AudioType` ✓（本文件用了 `using UnityEngine;` ✓），
// 裸写 `AudioType` 直接 CS0104 二义 ✗ —— 本文件一律指**项目那个** ✓（和 CaseDirector 第 11 行同一招 ✓）。
using AudioType = Project.Core.Runtime.Framework.AudioType;

namespace Project.Gameplay.Scripts.Case
{
    /// <summary>
    /// **一条读数有多强** ✓ —— 照 Docs/ContainmentRules.md §2 两张表分三档 ✓
    ///（EMF 5 / 4 / 1 ✓、温度 明显低 / 略低 / 正常 ✓、录音 嘶吼 / 嗡鸣 / 底噪 ✓）。
    ///
    /// 档位由"这件家具离**主场**多远"算出来 ✓（见 `CaseDirector` 的强度模型 ✓）——
    /// 档位决定**语义** ✓（强 = 够硬 ✓、弱 = 需要旁证 ✓），
    /// 而**具体数值和措辞每局都不同** ✓（档位下面挂的是候选行 ✓，见 <see cref="ReadingVariant"/> ✓）。
    /// </summary>
    public enum ReadingStrength
    {
        /// <summary>正常 ✓（离源头远 ✓，或者它是**诱饵** ✓）。</summary>
        Normal = 0,

        /// <summary>偏弱 ✓（有点不对劲 ✓，但光靠它不够 ✓）—— §2 里"需要旁证"那档 ✓。</summary>
        Weak = 1,

        /// <summary>强 ✓（一眼就是它 ✓）—— §2 里"可以直接下结论"那档 ✓。</summary>
        Strong = 2,
    }

    /// <summary>
    /// **一条候选读数** ✓ —— 同一个档位下可以挂好几条 ✓，开局**按种子挑一条** ✓、
    /// 再在它自己的 <see cref="Min"/>~<see cref="Max"/> 里**摇一个数** ✓，把数写进 <see cref="Text"/> 的 `{0}` ✓。
    ///
    /// 为什么要有它 ✗→✓：以前每个档位只有**一组写死的数值 + 一句话** ✗ ——
    /// 于是"温度枪：-2℃"和"温度枪：-2℃，比周围明显低。"每局都一模一样 ✗。
    /// 现在语义（档位 ✓）还是可控的 ✓，但**读数本身是摇出来的** ✓。
    /// </summary>
    [Serializable]
    public struct ReadingVariant
    {
        [Tooltip("数值下限 ✓（不用的种类留 0 ✓）。")]
        public float Min;

        [Tooltip("数值上限 ✓。和下限填一样 = 固定值 ✓（EMF 5 级这种就别摇 ✗，5 就是 5 ✓）。")]
        public float Max;

        [Tooltip("布尔观测 ✓（紫外线用 ✓）。")]
        public bool Flag;

        [Tooltip("录到的声音 ✓（录音用 ✓）。")]
        public AudioType Audio;

        [TextArea]
        [Tooltip("文案 ✓。**`{0}` = 这一局摇出来的数值** ✓（例如「温度枪：`{0}`℃，…」✓）；\n"
                 + "不需要数值就别写占位符 ✓（录到嘶吼那种 ✓）。")]
        public string Text;

        /// <summary>在 [Min, Max] 里摇一个值 ✓（上下限相同就是固定值 ✓）。</summary>
        public float Roll(CaseRandom rng)
        {
            return Min >= Max ? Min : Min + ((Max - Min) * (float)rng.NextDouble());
        }

        /// <summary>把摇出来的数写进文案 ✓（<paramref name="valueText"/> 已经按小数位格式化好 ✓）。</summary>
        public string BuildText(string valueText)
        {
            if (string.IsNullOrEmpty(Text))
            {
                return string.Empty;
            }

            return Text.IndexOf("{0}", StringComparison.Ordinal) >= 0
                ? Text.Replace("{0}", valueText)
                : Text;
        }
    }

    /// <summary>
    /// **一种读数的完整定义** ✓ —— "加新读数"的唯一入口 ✓。
    ///
    /// 以前一种读数的信息散在 6 个 switch 上 ✗（工具 ✗、文案 ✗、观测值 ✗、日志名 ✗ …），
    /// 加一种要改 8 处 ✗；现在一行就是一种读数 ✓，
    /// 而且每档下面**挂几条候选就有几种随机结果** ✓（加候选不用动代码 ✓）。
    /// </summary>
    [Serializable]
    public struct ReadingKindDefinition
    {
        [Tooltip("读数种类 ✓（也决定写到哪个观测位 ✓）。")]
        public CaseReadingKind Kind;

        [Tooltip("用哪把工具读 ✓。")]
        public ToolType Tool;

        [Tooltip("日志 / 提示里显示的工具名 ✓，例如「温度枪」。")]
        public string DisplayName;

        [Range(0f, 2f)]
        [Tooltip("**这条读数多灵敏** ✓：1 = 标准 ✓、>1 = 远处也能读到东西（探测器 ✓）、\n"
                 + "<1 = 迟钝（工具包 ✓，只有贴到源头才有反应 ✓）。")]
        public float Sensitivity;

        [Tooltip("文案里数值保留几位小数 ✓（温度 1 ✓、EMF 0 ✓）。")]
        public int Decimals;

        [Header("强（离源头很近 ✓）")]
        public ReadingVariant[] StrongVariants;

        [Header("弱（有点不对劲 ✓，需要旁证 ✓）")]
        public ReadingVariant[] WeakVariants;

        [Header("正常（离源头远 ✓，或者是诱饵 ✓）")]
        public ReadingVariant[] NormalVariants;

        public ReadingVariant[] Variants(ReadingStrength strength) => strength switch
        {
            ReadingStrength.Strong => StrongVariants,
            ReadingStrength.Weak => WeakVariants,
            _ => NormalVariants,
        };

        /// <summary>按种子在这一档里挑一条候选 ✓（这档没配就回落到另一档 ✓，免得读出空白 ✗）。</summary>
        public ReadingVariant Pick(ReadingStrength strength, CaseRandom rng)
        {
            var pool = Variants(strength);

            if (pool == null || pool.Length == 0)
            {
                pool = strength == ReadingStrength.Normal ? StrongVariants : NormalVariants;
            }

            return pool == null || pool.Length == 0 ? default : pool[rng.Next(pool.Length)];
        }

        /// <summary>把数值按本种类的小数位格式化 ✓（`{0}` 用的就是它 ✓）。</summary>
        public string FormatNumber(float value) => value.ToString("F" + Mathf.Clamp(Decimals, 0, 3));
    }

    /// <summary>
    /// **某件家具对某种读数的专属文案** ✓（三档各一条 ✓）—— 只给"值得单独写"的家具填 ✓。
    /// 同样支持 `{0}` ✓；没填的家具自动落到种类表的候选 ✓。
    /// </summary>
    [Serializable]
    public struct FurnitureReadingOverride
    {
        [Tooltip("家具 id 里含这个关键词才生效 ✓（和 containmentHostKeywords 同一套写法 ✓，例如「沙发」✓）。")]
        public string HostKeyword;

        [Tooltip("覆盖哪一种读数 ✓。")]
        public CaseReadingKind Kind;

        [TextArea] public string StrongText;
        [TextArea] public string WeakText;
        [TextArea] public string NormalText;

        public string Text(ReadingStrength strength) => strength switch
        {
            ReadingStrength.Strong => StrongText,
            ReadingStrength.Weak => WeakText,
            _ => NormalText,
        };
    }

    /// <summary>
    /// **读数种类表** ✓（菜单 `Tools/Project/Case/Create Reading Kind Table` 生成 ✓）。
    ///
    /// `CaseDirector` 遍历这张表生成每件家具的整套读数 ✓；**没接资产也能跑** ✓ ——
    /// <see cref="CreateDefaultInstance"/> 里有一份等价兜底 ✓。
    /// 嫌"读数还是固定的"就在这里给每档**多加几条候选** ✓（加候选不用改代码 ✓）。
    /// </summary>
    [CreateAssetMenu(menuName = "Project/Case/Reading Kind Table", fileName = "ReadingKindTable")]
    public sealed class ReadingKindTable : ScriptableObject
    {
        [SerializeField] private ReadingKindDefinition[] kinds = new ReadingKindDefinition[0];
        [SerializeField] private FurnitureReadingOverride[] furnitureOverrides = new FurnitureReadingOverride[0];

        public IReadOnlyList<ReadingKindDefinition> Kinds => kinds;

        public bool TryGet(CaseReadingKind kind, out ReadingKindDefinition definition)
        {
            for (var i = 0; i < kinds.Length; i++)
            {
                if (kinds[i].Kind == kind)
                {
                    definition = kinds[i];
                    return true;
                }
            }

            definition = default;
            return false;
        }

        /// <summary>这件家具这条读数有没有专属文案 ✓（按关键词匹配家具 id ✓，取最靠前的那条 ✓）。</summary>
        public bool TryGetOverrideText(
            string interactableId, CaseReadingKind kind, ReadingStrength strength, string valueText, out string text)
        {
            text = null;

            if (string.IsNullOrEmpty(interactableId))
            {
                return false;
            }

            for (var i = 0; i < furnitureOverrides.Length; i++)
            {
                var entry = furnitureOverrides[i];
                if (entry.Kind != kind || string.IsNullOrEmpty(entry.HostKeyword))
                {
                    continue;
                }

                if (interactableId.IndexOf(entry.HostKeyword, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                var raw = entry.Text(strength);
                if (string.IsNullOrEmpty(raw))
                {
                    return false;
                }

                text = raw.Replace("{0}", valueText);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 代码里的**兜底表** ✓（= 项目现在这 5 种读数 ✓，三档口径照 Docs/ContainmentRules.md §2 ✓）。
        ///
        /// 每档挂 3 条候选 ✓ —— 这就是"读数随机"的来源 ✓：
        /// 挑哪条按种子 ✓、数值在候选自己的范围里摇 ✓。
        /// **想更随机就往这里（或资产里）多写候选** ✓，不用改代码 ✓。
        /// </summary>
        public static ReadingKindTable CreateDefaultInstance()
        {
            var table = CreateInstance<ReadingKindTable>();

            table.kinds = new[]
            {
                new ReadingKindDefinition
                {
                    Kind = CaseReadingKind.Temperature,
                    Tool = ToolType.Thermometer,
                    DisplayName = "温度枪",
                    Sensitivity = 0.9f,
                    Decimals = 1,
                    StrongVariants = new[]
                    {
                        Variant(-4f, 0f, "温度枪：{0}℃，比周围明显低。"),
                        Variant(-9f, -3f, "温度枪：{0}℃，探头刚放上去读数就往下掉。"),
                        Variant(-6f, -1f, "温度枪：{0}℃，这块地方像是被冻过。"),
                    },
                    WeakVariants = new[]
                    {
                        Variant(4f, 11f, "温度枪：{0}℃，比房间其他地方凉一点。"),
                        Variant(6f, 14f, "温度枪：{0}℃，凉，但说不上哪里不对。"),
                        Variant(3f, 9f, "温度枪：{0}℃，贴久了才看出偏低。"),
                    },
                    NormalVariants = new[]
                    {
                        Variant(21f, 26f, "温度枪：{0}℃，就是这个房间的温度。"),
                        Variant(19f, 25f, "温度枪：{0}℃，很正常。"),
                        Variant(22f, 27f, "温度枪：{0}℃，没有异常。"),
                    },
                },
                new ReadingKindDefinition
                {
                    Kind = CaseReadingKind.Emf,
                    Tool = ToolType.Detector,
                    DisplayName = "探测器",
                    Sensitivity = 1.2f, // 最灵敏 ✓：远处也能读到一点 ✓
                    Decimals = 0,
                    StrongVariants = new[]
                    {
                        Variant(5f, 5f, "便携式探测器：EMF 5 级 —— 贴上去就一直在响。"),
                        Variant(5f, 5f, "便携式探测器：EMF 5 级 —— 指针直接打到底。"),
                    },
                    WeakVariants = new[]
                    {
                        Variant(4f, 4f, "便携式探测器：EMF 4 级，指针抖得厉害。"),
                        Variant(3f, 4f, "便携式探测器：EMF {0} 级，有波动但没到顶。"),
                    },
                    NormalVariants = new[]
                    {
                        Variant(1f, 1f, "便携式探测器：EMF 1 级，还在正常范围里。"),
                        Variant(1f, 2f, "便携式探测器：EMF {0} 级，正常。"),
                    },
                },
                new ReadingKindDefinition
                {
                    Kind = CaseReadingKind.Audio,
                    Tool = ToolType.Recorder,
                    DisplayName = "录音笔",
                    Sensitivity = 0.8f,
                    Decimals = 0,
                    StrongVariants = new[]
                    {
                        Variant(0f, 0f, "录音笔：男人的嘶吼和惨叫，重复了三遍。", audio: AudioType.Scream),
                        Variant(0f, 0f, "录音笔：一段压低了的哭声，越听越不像人。", audio: AudioType.Scream),
                        Variant(0f, 0f, "录音笔：有东西在很近的地方喘气。", audio: AudioType.Whisper),
                    },
                    WeakVariants = new[]
                    {
                        Variant(0f, 0f, "录音笔：一段低频嗡鸣，像是有什么在共振。", audio: AudioType.Hum),
                        Variant(0f, 0f, "录音笔：断断续续的杂音，听不出是什么。", audio: AudioType.Hum),
                        Variant(0f, 0f, "录音笔：很轻的嗡声，几乎被底噪盖住。", audio: AudioType.Hum),
                    },
                    NormalVariants = new[]
                    {
                        Variant(0f, 0f, "录音笔：只有很轻的电流底噪，听不出别的。", audio: AudioType.Normal),
                        Variant(0f, 0f, "录音笔：安静的室内底噪，没有别的。", audio: AudioType.Normal),
                    },
                },
                new ReadingKindDefinition
                {
                    Kind = CaseReadingKind.Uv,
                    Tool = ToolType.UVLight,
                    DisplayName = "紫外线灯",
                    Sensitivity = 0.7f,
                    Decimals = 0,
                    StrongVariants = new[]
                    {
                        Variant(0f, 0f, "紫外线灯：一大片擦不掉的暗红痕迹。", true),
                        Variant(0f, 0f, "紫外线灯：整块表面都在发荧光。", true),
                    },
                    WeakVariants = new[]
                    {
                        Variant(0f, 0f, "紫外线灯：几处很淡的荧光点，看不太清。", true),
                        Variant(0f, 0f, "紫外线灯：边缘有一点残留，像是被擦过。", true),
                    },
                    NormalVariants = new[]
                    {
                        Variant(0f, 0f, "紫外线灯：没有残留痕迹。"),
                        Variant(0f, 0f, "紫外线灯：干干净净，什么都没照出来。"),
                    },
                },
                new ReadingKindDefinition
                {
                    Kind = CaseReadingKind.Physical,
                    Tool = ToolType.ToolKit,
                    DisplayName = "工具包",
                    Sensitivity = 0.5f, // 最迟钝 ✓：只有贴到源头才拆得出东西 ✓
                    Decimals = 0,
                    StrongVariants = new[]
                    {
                        Variant(0f, 0f, "工具包：夹层里塞着一团不该在的头发。"),
                        Variant(0f, 0f, "工具包：里面粘着一层干掉的、发黑的东西。"),
                        Variant(0f, 0f, "工具包：夹层缝着几根很长的头发，像是故意藏的。"),
                    },
                    WeakVariants = new[]
                    {
                        Variant(0f, 0f, "工具包：夹层有点不对劲，但翻不出什么。"),
                        Variant(0f, 0f, "工具包：拆开看了一圈，只有一点说不清的碎屑。"),
                    },
                    NormalVariants = new[]
                    {
                        Variant(0f, 0f, "工具包：就是普通的填充物，没有别的。"),
                        Variant(0f, 0f, "工具包：拆开也没有夹层，干净。"),
                    },
                },
            };

            // 先填两件"招牌"家具 ✓（§3.3 ✓：沙发 ✓、书架底层那个玻璃瓶 ✓）—— 三档各一条 ✓。
            table.furnitureOverrides = new[]
            {
                new FurnitureReadingOverride
                {
                    HostKeyword = "沙发",
                    Kind = CaseReadingKind.Audio,
                    StrongText = "录音笔：男人的嘶吼和惨叫，重复了三遍，最后一句像是在喊一个名字。",
                    WeakText = "录音笔：断断续续的喘气声，听不出是谁。",
                    NormalText = "录音笔：平稳的女性呼吸声，没有别的。",
                },
                new FurnitureReadingOverride
                {
                    HostKeyword = "沙发",
                    Kind = CaseReadingKind.Emf,
                    StrongText = "便携式探测器：EMF 5 级 —— 贴上去就一直在响。",
                    WeakText = "便携式探测器：EMF 4 级，指针抖得厉害。",
                    NormalText = "便携式探测器：EMF 1 级，正常。",
                },
                new FurnitureReadingOverride
                {
                    HostKeyword = "书籍",
                    Kind = CaseReadingKind.Temperature,
                    StrongText = "温度枪：{0}℃，玻璃外壁结了一层霜。",
                    WeakText = "温度枪：{0}℃，比书架其他地方凉。",
                    NormalText = "温度枪：{0}℃，和室温一样。",
                },
                new FurnitureReadingOverride
                {
                    HostKeyword = "书籍",
                    Kind = CaseReadingKind.Audio,
                    StrongText = "录音笔：一段低频嗡鸣，像是玻璃自己在共振。",
                    WeakText = "录音笔：很轻的嗡声，几乎被底噪盖住。",
                    NormalText = "录音笔：很轻的电流底噪，听不出别的。",
                },
            };

            return table;
        }

        /// <summary>写候选行的小工具 ✓（让上面那张默认表能一眼看出"这档有几种结果" ✓）。</summary>
        private static ReadingVariant Variant(float min, float max, string text, bool flag = false, AudioType audio = AudioType.None)
        {
            return new ReadingVariant
            {
                Min = min,
                Max = max,
                Flag = flag,
                Audio = audio,
                Text = text,
            };
        }
    }
}
