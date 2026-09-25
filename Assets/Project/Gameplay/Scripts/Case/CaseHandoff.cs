using Project.Gameplay.Scripts.Items;
using UnityEngine;

namespace Project.Gameplay.Scripts.Case
{
    /// <summary>
    /// 序幕 → 关卡的**交接单**（跨场景传参）。
    ///
    /// 为什么是静态的：序幕那边的 manager 都是**场景级**的，跟着场景一起销毁，
    /// 挂不住任何需要跨场景的东西；而这份数据只在"切场景的那一瞬间"有效，所以一个静态槽位最省事。
    ///
    /// 传什么：
    /// - <see cref="Seed"/>：本局种子 —— 有了它，关卡不再自己摇，**这一关是确定的**（序幕定，关卡照办）。
    /// - <see cref="Loadout"/>：已经装备好的那几件工具 —— 这样**序幕 UI 上显示的道具和关卡里的完全一致**
    ///   （否则两边各自从 5 件里挑，很容易差一件）。
    ///
    /// 生命周期：序幕在窗口 UI 填充时 <see cref="Publish"/>，关卡在 <see cref="CaseDirector.BeginCase"/>
    /// 里取用后立刻 <see cref="Clear"/> —— 只生效一次，避免下次进关卡又被上一局的参数影响。
    /// </summary>
    public static class CaseHandoff
    {
        /// <summary>有没有待处理的交接单。</summary>
        public static bool HasRequest { get; private set; }

        /// <summary>本局种子（没交接单时无意义）。</summary>
        public static int Seed { get; private set; }

        /// <summary>已经装备好的工具（没交接单时为空）。</summary>
        public static ToolItem[] Loadout { get; private set; } = new ToolItem[0];

        /// <summary>
        /// 序幕收屏那一刻，雷达扇形扫到的角度（度）。
        /// 关卡第一帧把扇形放到同一个角度，于是面板内 CRT 收屏 → 展开的那一下，
        /// 看起来就是**同一次旋转没断过** ✓。
        /// 这个值**不随 <see cref="Clear"/> 清掉**：它只是一次视觉衔接，留着也不影响后面的对局。
        /// </summary>
        public static float RadarSweepAngle { get; private set; } = -1f;

        /// <summary>序幕侧调用：记下当前雷达角度（要在切场景之前调）。</summary>
        public static void PublishRadarAngle(float degrees)
        {
            RadarSweepAngle = degrees;
            Debug.Log($"[Handoff] 记下序幕雷达角度 {degrees:0.#}°，关卡会从同一角度接着转。");
        }

        /// <summary>序幕侧调用：把"这一关是固定的 + 带哪几件"写进交接单。</summary>
        public static void Publish(int seed, ToolItem[] loadout)
        {
            Seed = seed;
            Loadout = loadout ?? new ToolItem[0];
            HasRequest = true;

            Debug.Log($"[Handoff] 序幕已把本关交给关卡：种子 {seed}，带进场 {Loadout.Length} 件"
                      + "（关卡会照这个来，不再自己摇）。");
        }

        /// <summary>关卡侧取用后清掉（只生效一次）。</summary>
        public static void Clear()
        {
            HasRequest = false;
            Loadout = new ToolItem[0];
        }
    }
}
