using UnityEngine;

namespace Project.Core.Runtime.Managers
{
    public sealed class GameLoopManager : ManagerBehaviour
    {
        public string CurrentLevelId { get; private set; }

        public void StartLevel(string levelId)
        {
            CurrentLevelId = levelId;
            Debug.Log($"StartLevel: {levelId}");
        }
    }
}
