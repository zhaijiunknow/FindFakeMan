using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 收容箱槽位按钮（boxButton）：运行时读取 InventoryManager 收容箱该槽位的线索，显示到详情区。
    /// 收容箱默认为空，收容到的线索按顺序（slotIndex）填入；不预绑任何 Item。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class ContainmentSlotButton : MonoBehaviour
    {
        [SerializeField] private int slotIndex;             // 收容箱槽位（0..capacity-1）
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

            var items = inventory.GetContainmentItems();
            var item = slotIndex >= 0 && slotIndex < items.Count ? items[slotIndex] : null;
            detail?.ShowItem(item);
        }
    }
}
