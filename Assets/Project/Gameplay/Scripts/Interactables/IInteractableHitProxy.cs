using UnityEngine;

namespace Project.Gameplay.Scripts.Interactables
{
    /// <summary>
    /// 「落点代理」：盖住整个房间的整帧图（典型就是特写层）用它告诉工具拖拽 ——
    /// "你现在看着的，其实是我代表的这件家具"。
    ///
    /// 为什么需要它：家具的点击判定是**贴着自己那张透明图**的 alpha 算的（见 README 的"图自己就是按钮"）。
    /// 一旦切到特写，屏幕上看到的抽屉/沙发局部和底下那些图的形状就对不上了 ——
    /// 玩家很自然会在特写正中松手，而那一点在底下可能是透明的，于是变成"松手的地方没有可以调查的东西"。
    /// 所以特写显示期间，落点解析应该认这张特写代表的家具，而不是认坐标。
    ///
    /// 实现放 UI 层（<c>RoomCloseUpView</c>）：Gameplay 定义接口、不认识 UI，依赖方向仍然是 UI → Gameplay。
    /// </summary>
    public interface IInteractableHitProxy
    {
        /// <summary>当前这块图代表的交互物；没有（图没显示 / 没有关联家具）时返回 null。</summary>
        SimpleInteractable HitTarget { get; }
    }
}
