using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Project.Core.Runtime.Managers
{
    public sealed class SceneFlowManager : ManagerBehaviour
    {
        public async UniTask LoadSceneAsync(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            var operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                return;
            }

            await operation.ToUniTask();
        }
    }
}
