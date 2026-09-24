using System.Collections.Generic;

namespace Project.Gameplay.Scripts.Case
{
    /// <summary>
    /// 项目自己的种子随机：**完全可知、可复现**，而且是 <see cref="System.Random"/> 的直接替代
    /// （继承它并覆盖 Next / NextDouble，所以现有调用点和函数签名一行都不用改）。
    ///
    /// 算法只有一行（LCG，Numerical Recipes 的常量）：
    /// <code>state = state * 1664525 + 1013904223;</code>（uint 溢出即取模 2^32）
    /// - <see cref="NextDouble"/> 取 state 的高 24 位 —— 所以"种子 → 第一个数"是可以手算/逐步验证的；
    /// - 同一颗种子永远同一个序列，**不依赖 .NET / Unity 的版本实现**（这就是"完全可知"的意义）。
    ///
    /// 另外提供 <see cref="For"/>：用「种子 + 用途标签」开一条**独立**的流。
    /// 这样某个决策改不改（比如身份从"随机摇"改成"直接指定"）**不会连带挪动**别的决策 ——
    /// 这正是 System.Random 那种"一条流按调用顺序消耗"最容易踩的坑（我们前面就踩过一次 ✗）。
    /// </summary>
    public sealed class CaseRandom : System.Random
    {
        private uint state;

        public CaseRandom(int seed)
        {
            // 0 是弱初态，换一个非零常数起手；再过一次雪崩混合（见 Mix 的注释）。
            state = Mix(seed == 0 ? 0x9E3779B9u : unchecked((uint)seed));
        }

        private CaseRandom(uint initialState)
        {
            state = initialState;
        }

        /// <summary>
        /// 雪崩混合（lowbias32 的 finalizer，三步异或+乘法）。
        ///
        /// 为什么必须要它：LCG 的**高位对种子的低位变化不敏感** —— 直接拿 2000/2001/2002 当种子，
        /// 它们第一个随机数几乎一样，于是"身份"会成块出现（实测 2023~2030 连着 8 个都是伪人 ✗）。
        /// 更麻烦的是布局也用同一颗种子起流，相邻种子的关卡会长得很像。
        /// 混一下之后，相邻种子会产生完全不同的序列，而结果依然是**确定、可复现、可核对**的。
        /// </summary>
        private static uint Mix(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }

        /// <summary>种子 + 用途标签 → 一条独立的随机流（同种子同标签，结果永远相同）。</summary>
        public static CaseRandom For(int seed, string channel)
        {
            // FNV-1a：把标签混进种子，作为这条流的初态
            var hash = 2166136261u;
            unchecked
            {
                hash = (hash ^ (uint)seed) * 16777619u;
                if (!string.IsNullOrEmpty(channel))
                {
                    foreach (var c in channel)
                    {
                        hash = (hash ^ c) * 16777619u;
                    }
                }
            }

            // 混完之后再过一次雪崩混合：相邻种子（2000/2001/…）的流才会完全不一样。
            var rng = new CaseRandom(Mix(hash));
            rng.NextUInt(); // 先走一步，避免弱初态
            return rng;
        }

        /// <summary>推进一格并给出 32 位状态（想自己核对序列时用它）。</summary>
        public uint NextUInt()
        {
            unchecked
            {
                state = state * 1664525u + 1013904223u;
            }

            return state;
        }

        public override int Next() => (int)(NextUInt() & 0x7FFFFFFFu);

        public override int Next(int maxValue) => maxValue <= 0 ? 0 : (int)(NextDouble() * maxValue);

        public override int Next(int minValue, int maxValue)
        {
            if (maxValue <= minValue)
            {
                return minValue;
            }

            return minValue + (int)(NextDouble() * (maxValue - minValue));
        }

        /// <summary>0 ≤ x &lt; 1（取高 24 位，所以精度和取值都可手算）。</summary>
        public override double NextDouble() => (NextUInt() >> 8) * (1.0 / 16777216.0);

        /// <summary>概率判定：等价于 <c>rng.NextDouble() &lt; probability</c>。</summary>
        public bool Chance(double probability) => NextDouble() < probability;

        /// <summary>就地洗牌（Fisher-Yates，用的也是这条流）。</summary>
        public void Shuffle<T>(IList<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
