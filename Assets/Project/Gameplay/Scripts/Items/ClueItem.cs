using UnityEngine;

namespace Project.Gameplay.Scripts.Items
{
    [CreateAssetMenu(menuName = "Project/Items/Clue Item")]
    public class ClueItem : Item
    {
        [SerializeField] private bool isAnomaly;
        [SerializeField] private bool requiresContainment;
        [SerializeField] private string evidenceId;

        public bool IsAnomaly => isAnomaly;
        public bool RequiresContainment => requiresContainment;
        public string EvidenceId => evidenceId;
    }
}
