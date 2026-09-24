using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 检视面板的「关闭」按钮：收面板 + 把状态从 Inspection 退回探索。
    ///
    /// 自己绑 onClick（和项目里的 <c>ButtonAction</c> 一个套路）：这样不用在编辑期用
    /// <c>UnityEventTools.AddPersistentListener</c> 去写持久监听，少一层可能出错的接线。
    ///
    /// 退状态这件事其实由 <see cref="InvestigationHudView.HideInspector"/> 统一负责
    /// （它是所有收面板路径的必经点），这里只负责"请求收面板"。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class InspectorCloseButton : MonoBehaviour
    {
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(OnClickCloseInspector);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClickCloseInspector);
            }
        }

        public void OnClickCloseInspector()
        {
            if (Services.TryGet<UIManager>(out var uiManager))
            {
                uiManager.HideInspector();
                return;
            }

            Debug.LogWarning("[HUD] 场景里没有 UIManager，无法关闭检视面板。");
        }
    }
}
