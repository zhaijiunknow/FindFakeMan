using System.Collections.Generic;
using UnityEngine;

namespace Project.Gameplay.Scripts.Case
{
    /// <summary>
    /// 随机数「桶」：**同一组共用一个流**，不同组互相独立。
    ///
    /// 为什么要分组：一条流按调用顺序消耗时，任何一处"多摇一次"都会把它**后面所有结果**挪位 ✗
    /// （我们前面就踩过：加一次身份判定，异常分布跟着变了）。分组之后：
    /// - 组**之间**互不影响（在案情里加一次随机，不会动到装备/身份）；
    /// - 组**内部**保持顺序消耗（这正是"一个桶"的语义，同一组的东西本来就有先后关系）。
    ///
    /// 用法：
    /// <code>
    /// CaseRandomBuckets.BeginSession(seed);                        // 一局开始 / 读档恢复时调一次
    /// var rng = CaseRandomBuckets.Bucket(CaseGroups.Loadout);      // 拿本组的桶（第一次取时创建）
    /// var slot = rng.Next(5);                                      // 在本组流里推进
    /// </code>
    /// 组名一律用 <see cref="CaseGroups"/> 里的常量（或 <see cref="CaseGroups.Furniture"/>），别手写字符串。
    ///
    /// 关于层级：这个类和 <see cref="CaseRandom"/> 都在 Gameplay 下，但 Core 也在用
    /// （<c>BranchManager</c> 播种）—— 本项目 Core→Gameplay 的引用本来就有先例（InteractionManager 用
    /// SampleInteractableRule），所以先这样；等哪天拆分程序集，把它俩一起挪到
    /// <c>Project.Core.Runtime.Framework</c> 即可。
    /// </summary>
    public static class CaseRandomBuckets
    {
        private static readonly Dictionary<string, CaseRandom> buckets = new();

        /// <summary>本局种子（没 <see cref="BeginSession"/> 过时是 0）。</summary>
        public static int SessionSeed { get; private set; }

        /// <summary>新的一局 / 读档恢复：换种子并清空所有桶（桶里的进度不该跨局带着走）。</summary>
        public static void BeginSession(int seed)
        {
            SessionSeed = seed;
            buckets.Clear();
            Debug.Log($"[Random] 本局随机桶已重置：种子 {seed}（分组：{CaseGroups.All}；同组共用一个流，组间独立）。");
        }

        /// <summary>取某个分组的随机桶（第一次取时按「种子 + 组名」创建）。</summary>
        public static CaseRandom Bucket(string group)
        {
            var key = string.IsNullOrEmpty(group) ? CaseGroups.Case : group;
            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = CaseRandom.For(SessionSeed, key);
                buckets[key] = bucket;
            }

            return bucket;
        }

        /// <summary>只清空桶、不改种子（想拿同一颗种子从头摇一遍时用）。</summary>
        public static void Clear()
        {
            buckets.Clear();
        }
    }

    /// <summary>随机分组的组名（集中在这里，免得各处手写字符串拼错）。</summary>
    public static class CaseGroups
    {
        /// <summary>案情总流：异常件数、挑哪几件是异常的、整体布局。</summary>
        public const string Case = "case";

        /// <summary>本局身份（伪人 / 正常人）。</summary>
        public const string Identity = "identity";

        /// <summary>本局带哪几件工具。</summary>
        public const string Loadout = "loadout";

        /// <summary>环境音 / 氛围。</summary>
        public const string Ambience = "ambience";

        /// <summary>叙述分支 / 文本变体。</summary>
        public const string Narrative = "narrative";

        /// <summary>单件家具自己的流：读数数值、用哪把工具、要不要先检视 —— 按 id 分开，家具之间互不干扰。</summary>
        public static string Furniture(string interactableId) => $"furniture:{interactableId}";

        /// <summary>日志里给人看的分组一览。</summary>
        public const string All = "case / identity / loadout / furniture:* / ambience / narrative";
    }
}
