using UnityEngine;

namespace Project.UI.BigApp
{
    /// <summary>
    /// **已停用（空壳）✗** —— 结算不再单独开一屏。
    ///
    /// 原来它在 <c>Update</c> 里盯着 <c>CaseDirector.HasSubmitted</c> ✓，提交结论就自己搭出一层全屏黑板，
    /// 盖住整个窗口给"判定对不对 / 真相 / 读数 / 种子"和两个按钮（再调查一次、结束调查）✓。
    ///
    /// 现在这些内容全部搬到小软件「笔记」页里 ✓：
    ///  - 判定与真相由 <see cref="Project.Gameplay.Scripts.Case.CaseDirector.BuildJournalText"/> 追加「结论」段 ✓；
    ///  - 两个按钮由 <see cref="CaseJournalView"/> 把原来的「是伪人 / 是正常人」两格改成「再调查一次 / 结束调查」✓。
    ///
    /// 为什么玩家刚在笔记页下完结论、还要被一整屏黑糊脸 ✗：结果就长在他看的那一页上最自然 ✓。
    ///
    /// 保留这个空类只是为了让**旧场景里那块 CaseResult 物体**不报"脚本丢失"✗ ——
    /// 建造工具下次重建时会把那个物体从场景里删掉 ✓（BuildInvestigationScene 里的 RemoveCaseResultPanel ✓），
    /// 之后这个文件就真的可以删了 ✓。
    /// </summary>
    public sealed class CaseResultPanel : MonoBehaviour
    {
        // 故意什么都不做 ✓：没有 Update、没有 Update 里的自驱显示 ✗。
    }
}
