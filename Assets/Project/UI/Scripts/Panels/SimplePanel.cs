using System.Threading;
using Cysharp.Threading.Tasks;

namespace Project.UI.Panels
{
    /// <summary>
    /// 无逻辑的具体面板：纯内容面板直接用它，子类化 PanelBase 写生命周期逻辑。
    /// </summary>
    public sealed class SimplePanel : PanelBase
    {
        protected override UniTask OnOpenAsync(object data, CancellationToken ct) => UniTask.CompletedTask;
        protected override UniTask OnCloseAsync(object result, CancellationToken ct) => UniTask.CompletedTask;
        protected override UniTask OnPushedAsync(PanelBase above, CancellationToken ct) => UniTask.CompletedTask;
        protected override UniTask OnPoppedAsync(PanelBase above, CancellationToken ct) => UniTask.CompletedTask;
    }
}
