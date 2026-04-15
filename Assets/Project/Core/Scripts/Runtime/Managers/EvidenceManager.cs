using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Project.Core.Runtime.Framework;
using Project.Gameplay.Scripts.Items;

namespace Project.Core.Runtime.Managers
{
    public sealed class EvidenceManager : ManagerBehaviour, ISaveable<EvidenceSaveData>
    {
        public event Action<int, int> OnEvidenceCollected;
        public event Action OnGoalReached;

        private readonly HashSet<string> collectedEvidenceIds = new();
        private int goalCount;

        public void Initialize(int targetCount)
        {
            goalCount = Mathf.Max(0, targetCount);
            collectedEvidenceIds.Clear();
            NotifyChanged();
        }

        public bool AddEvidence(string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(evidenceId) || !collectedEvidenceIds.Add(evidenceId)) return false;
            NotifyChanged();
            CheckGoals();
            return true;
        }

        public void OnItemCollected(ClueItem clueItem)
        {
            if (clueItem != null)
            {
                AddEvidence(clueItem.EvidenceId);
            }
        }

        public void CheckGoals()
        {
            if (goalCount > 0 && collectedEvidenceIds.Count >= goalCount)
            {
                if (Services.TryGet<FlagManager>(out var flagManager))
                {
                    flagManager.Set("evidence_goal_reached");
                }
                OnGoalReached?.Invoke();
            }
        }

        public bool HasEvidence(string evidenceId) => collectedEvidenceIds.Contains(evidenceId);
        public IReadOnlyList<string> GetCollectedEvidenceIds() => collectedEvidenceIds.ToList();

        public EvidenceSaveData GetSaveData()
        {
            return new EvidenceSaveData { goalCount = goalCount, collectedEvidenceIds = collectedEvidenceIds.ToList() };
        }

        public void LoadState(EvidenceSaveData data)
        {
            collectedEvidenceIds.Clear();
            if (data == null) return;
            goalCount = data.goalCount;
            foreach (var id in data.collectedEvidenceIds)
            {
                collectedEvidenceIds.Add(id);
            }
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            OnEvidenceCollected?.Invoke(collectedEvidenceIds.Count, goalCount);
            Services.TryGet<UIManager>(out var uiManager);
            uiManager?.UpdateEvidenceDisplay(collectedEvidenceIds.Count, goalCount);
        }
    }
}
