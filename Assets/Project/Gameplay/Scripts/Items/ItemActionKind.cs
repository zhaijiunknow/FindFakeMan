namespace Project.Gameplay.Scripts.Items
{
    /// <summary>
    /// 精查面板上「往某个方向拖」能做的动作（设计表 §4.2 的四个方向、§3 的 ItemAction 雏形）。
    ///
    /// 设计表要求这些行为做成 ItemAction 派生类资源（PickupAction / DiscardAction / EquipAction / InspectAction…）。
    /// 项目里还没有那套资源体系，所以先用枚举 + <see cref="ItemActionRunner"/> 直接调 Manager。
    /// 将来换成资源时，把 Runner 里的调用搬进各个 Action 即可。
    /// </summary>
    public enum ItemActionKind
    {
        None = 0,
        Pickup = 1,   // 拾取：线索进收容箱/背包 + 记证据
        Discard = 2,  // 丢弃：异常线索会扣 SAN
        Inspect = 3,  // 检视：只是看，不改状态
        Equip = 4,    // 装备：工具进装备栏
    }
}
