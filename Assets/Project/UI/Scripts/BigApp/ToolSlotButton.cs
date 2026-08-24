using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 工具槽位按钮（itemButton）：运行时读取 InventoryManager 装备栏该槽位已携带的工具，显示到详情区。
    /// 工具栏默认为空，开场从背包(车)挑选携带的工具后动态填入；只在这些已携带的工具间切换，不预绑 Item。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class ToolSlotButton : MonoBehaviour
    {
        [SerializeField] private int slotIndex;             // 装备栏槽位（0..capacity-1）
        [SerializeField] private ItemDetailPanel detail;    // nothink 详情区

        private Button cachedButton;

        private void Awake()
        {
            cachedButton = GetComponent<Button>();
            cachedButton.onClick.AddListener(OnClicked);
        }

        private void OnDestroy()
        {
            if (cachedButton != null)
            {
                cachedButton.onClick.RemoveListener(OnClicked);
            }
        }

        private void OnClicked()
        {
            if (!Services.TryGet<InventoryManager>(out var inventory))
            {
                detail?.ShowItem(null);
                return;
            }

            var tool = inventory.GetEquippedTool(slotIndex);
            detail?.ShowItem(tool);
        }
    }
}
