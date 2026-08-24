namespace Project.UI.Panels
{
    /// <summary>
    /// 面板种类。
    /// Page = 全屏页面（压栈会隐藏下层页面）。
    /// Overlay = 模态浮层（压栈时下层保持可见、被底板盖住，且阻塞交互）。
    /// </summary>
    public enum PanelKind
    {
        Page,
        Overlay
    }
}
