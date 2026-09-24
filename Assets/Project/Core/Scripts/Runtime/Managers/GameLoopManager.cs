using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using UnityEngine;

namespace Project.Core.Runtime.Managers
{
    public sealed class GameLoopManager : ManagerBehaviour
    {
        public string CurrentLevelId { get; private set; }

        /// <summary>本局结算过没有。</summary>
        public bool CaseResolved { get; private set; }

        /// <summary>本局玩家判对了没有（没结算过时为 false）。</summary>
        public bool CaseCorrect { get; private set; }

        public void StartLevel(string levelId)
        {
            CurrentLevelId = levelId;
            CaseResolved = false;
            CaseCorrect = false;
            Debug.Log($"StartLevel: {levelId}");
        }

        /// <summary>
        /// 本局结算：玩家已经下了结论 —— 判对 → Victory，判错 → GameOver。
        /// 胜负状态本身还是由 <see cref="GameManager"/> 落（游戏循环只负责"这一局该收尾了"），
        /// 这样以后加"重开/返回"这些流程时有个统一入口。
        /// </summary>
        public void ResolveCase(bool correct)
        {
            CaseResolved = true;
            CaseCorrect = correct;

            if (Services.TryGet<GameManager>(out var gameManager))
            {
                if (correct)
                {
                    gameManager.TriggerVictory().Forget();
                }
                else
                {
                    gameManager.TriggerGameOver().Forget();
                }
            }

            Debug.Log($"[GameLoop] 本局结算：判定{(correct ? "正确" : "错误")} → {(correct ? "Victory" : "GameOver")}");
        }
    }
}
