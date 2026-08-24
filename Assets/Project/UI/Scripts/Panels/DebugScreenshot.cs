using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Project.UI.Panels
{
    /// <summary>临时调试：Play 时延迟截一张 Game view 存 PNG，用于检查 UI 排版。</summary>
    public sealed class DebugScreenshot : MonoBehaviour
    {
        public float delay = 1.5f;

        private async void Start()
        {
            await UniTask.Delay((int)(delay * 1000f));
            var path = Application.dataPath + "/../.unity/capture/bigsoftware_check.png";
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[DebugScreenshot] 已截图 {path}");
        }
    }
}
