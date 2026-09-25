using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 「道具」页里的一行按钮 ✓ —— 背包那列点一下 = 装进工具包 ✓，工具包那列点一下 = 放回背包 ✓。
    ///
    /// 为什么做成小组件、而不是给每行写一个固定方法 ✗：页里有 6 + 4 行 ✓，
    /// 用 lambda 接 onClick 就没法 RemoveListener ✗（项目的规矩是"固定方法 + Remove ✓"）；
    /// 做成组件之后每行自己在 <c>Awake</c> 里接一次 ✓，和工具槽 <see cref="ToolSlotButton"/> 一个套路 ✓。
    ///
    /// 两个字段由页视图在 <see cref="Configure"/> 里写入 ✓（编辑期由建造工具建行时调 ✓，
    /// 所以它们会被**序列化进场景** ✓ —— 运行时不需要再配一遍 ✓）。
    /// </summary>
    public sealed class ToolLoadoutRow : MonoBehaviour
    {
        [Tooltip("true = 背包那一列（点一下装进工具包 ✓）；false = 工具包那一列（点一下放回背包 ✓）。")]
        [SerializeField] private bool fromBackpack;

        [Tooltip("这一列里的第几行（0 起 ✓）。")]
        [SerializeField] private int index;

        /// <summary>由 <see cref="ToolLoadoutPageView.Build"/> 在建行时调 ✓（只在编辑期发生 ✓）。</summary>
        public void Configure(bool backpack, int rowIndex)
        {
            fromBackpack = backpack;
            index = rowIndex;
        }

        private void Awake()
        {
            var button = GetComponent<Button>();
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(OnClicked);
            button.onClick.AddListener(OnClicked);
        }

        private void OnClicked()
        {
            var page = GetComponentInParent<ToolLoadoutPageView>(true);
            if (page == null)
            {
                return;
            }

            if (fromBackpack)
            {
                page.EquipFromBackpack(index);
            }
            else
            {
                page.UnequipToBackpack(index);
            }
        }
    }
}
