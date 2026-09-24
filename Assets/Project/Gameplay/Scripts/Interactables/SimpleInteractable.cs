using UnityEngine;
using Project.Gameplay.Scripts.Items;

namespace Project.Gameplay.Scripts.Interactables
{
    public class SimpleInteractable : MonoBehaviour
    {
        [SerializeField] private string interactableId;
        [SerializeField] private Item associatedItem;
        [SerializeField] private bool isActive = true;
        [SerializeField] private bool isCollected;

        /// <summary>
        /// 正在被检视（详情区 / 特写正开着它）。
        /// **故意不序列化**：这是运行时状态，由 UI 层在开、关检视时写入（<c>InvestigationHudView.ShowInspector / HideInspector</c>）。
        /// 规则用它做"必须先看清才能用工具"的门槛（例如木桌抽屉：不放大看不清里面有什么）。
        /// </summary>
        private bool isInspected;

        /// <summary>玩家做过「检视」（右拖）—— 只有它能让房间切到特写。同样不序列化。</summary>
        private bool isZoomed;

        [SerializeField] private string interactionState = "default";
        [SerializeField] [TextArea] private string description;
        [SerializeField] [TextArea] private string anomalyDescription;

        public string InteractableId => interactableId;
        public Item AssociatedItem => associatedItem;
        public bool IsActive => isActive;
        public bool IsCollected => isCollected;

        /// <summary>正在被检视吗（详情区 / 特写开着它）。</summary>
        public bool IsInspected => isInspected;
        public string InteractionState => interactionState;
        public string Description => description;
        public string AnomalyDescription => anomalyDescription;

        public void SetActive(bool value)
        {
            isActive = value;
        }

        /// <summary>开/关检视时由 UI 层写。收起检视一定要清掉，否则"先看清才能用工具"的门槛会一直开着。</summary>
        public void SetInspected(bool value)
        {
            isInspected = value;
        }

        /// <summary>
        /// 玩家是否真的做过「检视」这个动作（右拖）。
        /// **点一下只是选中 + 开详情区**，不算；特写放大、以及"先看清才能用工具"的门槛都看这个标记。
        /// </summary>
        public bool IsZoomed => isZoomed;

        public void SetZoomed(bool value)
        {
            isZoomed = value;
        }

        public void SetCollected()
        {
            isCollected = true;
            isActive = false;
        }

        public void SetInteractionState(string state)
        {
            interactionState = state;
        }
    }
}
