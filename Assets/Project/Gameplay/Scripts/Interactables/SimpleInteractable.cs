using UnityEngine;
using Project.Gameplay.Scripts.Items;

namespace Project.Gameplay.Scripts.Interactables
{
    public class SimpleInteractable : MonoBehaviour
    {
        [SerializeField] private string interactableId;
        [SerializeField] private Item associatedItem;

        // 运行时塞进来的"这里现在能拿走什么" ✓（通用关卡器用：案情开局才知道哪儿有收容物 ✓）。
        private Item runtimeItem;
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
        /// <summary>
        /// 这里"可以拿走的东西"（详情区显示它 ✓，方向体感的「拾取」也靠它 ✓）。
        ///
        /// 通用关卡器改造之后它通常是**运行时塞进来的** ✓：玩家用对口道具读过一次之后，
        /// <see cref="SampleInteractableRule"/> 才把收容物塞进来 ✓（这就是"解锁拾取" ✓）。
        /// 好处是**不需要新状态位** ✓：没读过 → 这里是 null → 体感上的「拾取」本来就用不了 ✓；
        /// 场景里烘死的 <c>associatedItem</c> 只当兜底 ✓（旧内容 / 非通用关卡器的交互物 ✓）。
        /// </summary>
        public Item AssociatedItem => runtimeItem != null ? runtimeItem : associatedItem;

        /// <summary>运行时换掉"可以拿走的东西" ✓。传 null = 这里现在没东西可拿 ✓（拿走或丢弃之后都调它 ✓）。</summary>
        public void SetRuntimeItem(Item item) => runtimeItem = item;
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

        /// <summary>
        /// 标记"这里已经被查过 / 已经拿走" ✓。
        ///
        /// ⚠️ **不再顺手把交互物关掉** ✗→✓：以前这里还写了 `isActive = false;` ✗ ——
        /// 于是"读过一次"的家具立刻变得**点不动、也用不了工具** ✗（点击和用工具都要判 `IsActive` ✓），
        /// 表现就是"交互一次之后家具就死了"✗、"收容物解锁之后再也选不中那件家具"✗。
        /// "查过之后就不再能用"这种语义要由内容显式表达 ✓（规则的 `deactivateOnSuccess` ✓ 会去调 `SetActive(false)` ✓），
        /// 不该偷偷塞在"已收集"这个标记里 ✗。
        /// </summary>
        public void SetCollected()
        {
            isCollected = true;
        }

        public void SetInteractionState(string state)
        {
            interactionState = state;
        }
    }
}
