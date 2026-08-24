using UnityEngine;

namespace Project.UI.OpeningCinematic
{
    /// <summary>
    /// 原型菜单的按钮回调：让「交接后菜单可交互」有可观察的证据。
    /// 开始按钮切换一个信息面板，设置按钮只打日志。
    /// </summary>
    public sealed class PrototypeMenuHandler : MonoBehaviour
    {
        public GameObject infoPanel;

        public void OnStartClicked()
        {
            Debug.Log("[OpeningMenu] 开始游戏 被点击");
            if (infoPanel != null)
            {
                infoPanel.SetActive(!infoPanel.activeSelf);
            }
        }

        public void OnSettingsClicked()
        {
            Debug.Log("[OpeningMenu] 设置 被点击");
        }
    }
}
