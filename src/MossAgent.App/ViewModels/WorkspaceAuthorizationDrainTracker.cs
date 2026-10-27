using MossAgent.App.Browser;
using MossAgent.Application.Authorization;

namespace MossAgent.App.ViewModels;

/// <summary>
/// 将授权监视器的项目排空状态同步到侧栏项目行；事件回调先切回 UI 线程再更新行状态。
/// </summary>
internal sealed class WorkspaceAuthorizationDrainTracker : IDisposable
{
    private readonly ProjectAuthorizationMonitor _monitor;
    private readonly Func<Guid, WorkspaceProjectNavigationViewModel?> _findRow;

    public WorkspaceAuthorizationDrainTracker(ProjectAuthorizationMonitor monitor,
        Func<Guid, WorkspaceProjectNavigationViewModel?> findRow)
    {
        _monitor = monitor;
        _findRow = findRow;
        _monitor.DrainStateChanged += HandleDrainStateChanged;
    }

    public void Refresh(WorkspaceProjectNavigationViewModel row) =>
        row.AuthorizationDrain = _monitor.DrainState(row.Project.Id);

    public void Dispose() => _monitor.DrainStateChanged -= HandleDrainStateChanged;

    private void HandleDrainStateChanged(Guid projectId) =>
        _ = AvaloniaUiThread.RunAsync(() =>
        {
            if (_findRow(projectId) is { } row) Refresh(row);
        }, CancellationToken.None);
}
