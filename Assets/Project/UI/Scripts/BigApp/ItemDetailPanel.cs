using Project.Gameplay.Scripts.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 详情区：挂在 BigApp 的 nothink 圆角面板上，字段直接读取 nothink 的子物体
    /// （Name/Desc/Status 均为 TextMeshProUGUI）。点槽位按钮后在此显示道具信息。
    /// </summary>
    public sealed class ItemDetailPanel : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private TextMeshProUGUI statusText;
        [Tooltip("未选中任何道具时显示的空状态提示")]
        [SerializeField] private string emptyHint = "未选择道具";

        public void ShowItem(Item item)
        {
            if (item == null)
            {
                Clear();
                return;
            }

            if (icon != null) icon.sprite = item.Icon;
            if (nameText != null) nameText.text = item.DisplayName;
            if (descText != null) descText.text = item.Description;

            if (statusText != null)
            {
                statusText.text = item switch
                {
                    ClueItem clue => clue.IsAnomaly ? "【异常线索】需收容" : "【线索】",
                    ToolItem tool => $"工具  耐久 {tool.Durability}/{tool.MaxDurability}",
                    _ => "道具"
                };
            }
        }

        public void Clear()
        {
            if (icon != null) icon.sprite = null;
            if (nameText != null) nameText.text = emptyHint;
            if (descText != null) descText.text = string.Empty;
            if (statusText != null) statusText.text = string.Empty;
        }
    }
}
