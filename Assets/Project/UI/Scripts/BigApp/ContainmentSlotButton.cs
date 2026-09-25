using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Items;
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
            var item = default(Item);
            if (Services.TryGet<InventoryManager>(out var inventory))
            {
                var items = inventory.GetContainmentItems();
                item = slotIndex >= 0 && slotIndex < items.Count ? items[slotIndex] : null;
            }

            // **必须走 HUD 正门** ✓，不能只喂详情区 ✗：
            // 方向体感读的是 InvestigationHudView.CurrentItem ✓（InspectorDragHandler ✓），
            // 而 detail.ShowItem 只是把文字填进面板 ✓、**不会**设 CurrentItem ✗ ——
            // 结果就是"点开收容格 → 下拖丢弃"永远没反应（体感眼里的 item 是 null ✗）。
            var hud = GetComponentInParent<InvestigationHudView>(true);
            if (hud != null)
            {
                hud.ShowInspector(item, null);
                return;
            }

            // 没有 HUD（比如单独跑某个 UI 测试场景 ✓）就退回老行为 ✓。
            detail?.ShowItem(item);
        }
    }
}
