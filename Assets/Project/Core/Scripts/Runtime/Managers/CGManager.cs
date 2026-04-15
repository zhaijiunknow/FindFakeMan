using UnityEngine;

namespace Project.Core.Runtime.Managers
{
    public sealed class CGManager : ManagerBehaviour
    {
        public void ShowBackground(string backgroundId) => Debug.Log($"ShowBackground: {backgroundId}");
        public void PlayCG(string cgId, bool skippable = true) => Debug.Log($"PlayCG: {cgId}, skippable={skippable}");
        public void HideCG() => Debug.Log("HideCG");
    }
}
