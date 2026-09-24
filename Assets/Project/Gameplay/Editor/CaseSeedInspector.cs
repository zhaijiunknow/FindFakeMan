using System.Text;
using Project.Gameplay.Scripts.Case;
using UnityEditor;
using UnityEngine;

namespace Project.Gameplay.Editor
{
    /// <summary>
    /// 种子体检：用**游戏里同一套随机**（<see cref="CaseRandom"/>）把一段种子的结果算出来，
    /// 直接告诉你哪些种子是伪人、哪些是正常人。
    ///
    /// 因为随机是自己实现的（LCG + 按用途分流），这里的算法和 <c>CaseDirector</c> 逐字一致 ——
    /// 身份走的是 <c>CaseRandom.For(seed, "identity")</c> 这条独立流，所以这里算出来的就是游戏里的结果，
    /// 而且**改别的决策不会影响它**。
    ///
    /// 菜单：Tools/Project/Gameplay/Print Case Identity For Seeds
    /// 用法：跑一次看 Console，把想要的种子填到
    /// 序幕 `MainUI → ToolSlotIconsView → caseSeed`（走序幕以它为准），
    /// 或者直接进关卡时的 `CaseDirector → fixedSeed`。
    /// </summary>
    public static class CaseSeedInspector
    {
        private const int ScanFrom = 2000;
        private const int ScanTo = 2060;
        private const double FakeHumanChance = 0.5;

        [MenuItem("Tools/Project/Gameplay/Print Case Identity For Seeds")]
        private static void PrintIdentityBySeed()
        {
            var fakeSeeds = new StringBuilder();
            var normalSeeds = new StringBuilder();

            for (var seed = ScanFrom; seed <= ScanTo; seed++)
            {
                // 和 CaseDirector 一模一样：独立流 + Chance(fakeHumanChance)（默认 0.5）
                var isFake = CaseRandom.For(seed, "identity").Chance(FakeHumanChance);
                var target = isFake ? fakeSeeds : normalSeeds;
                target.Append(seed).Append(' ');
            }

            Debug.Log($"[CaseSeed] 种子 {ScanFrom}~{ScanTo}（fakeHumanChance={FakeHumanChance}，算法 = CaseRandom LCG）：\n"
                      + $"  伪人 = {fakeSeeds}\n"
                      + $"  正常人 = {normalSeeds}\n"
                      + "把想要的填到序幕的 ToolSlotIconsView.caseSeed（走序幕时以它为准），"
                      + "直接进关卡则填 CaseDirector.fixedSeed。");

            // 顺手把当前这条流的前几个数打出来，方便你对算法做手算核对。
            var probe = CaseRandom.For(ScanFrom, "identity");
            Debug.Log($"[CaseSeed] 种子 {ScanFrom} 的 identity 流前 3 个数："
                      + $"{probe.NextDouble():0.000000} / {probe.NextDouble():0.000000} / {probe.NextDouble():0.000000}"
                      + $"（均 < {FakeHumanChance} 才算伪人）");
        }
    }
}
