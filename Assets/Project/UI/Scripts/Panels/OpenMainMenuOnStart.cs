using UnityEngine;

namespace Project.UI.Panels
{
    /// <summary>
    /// 场景启动时自动打开主菜单页面（作为面板栈的栈底）。面板 ID 需与 PanelManager 注册表一致（main）。
    /// 放在运行时程序集（非 Editor 文件夹），避免场景脚本引用无效。
    /// </summary>
    public sealed class OpenMainMenuOnStart : MonoBehaviour
    {
        public PanelManager manager;

        private async void Start()
        {
            if (manager == null)
            {
                return;
            }

            await manager.OpenPanelAsync("main", null);
        }
    }
}
