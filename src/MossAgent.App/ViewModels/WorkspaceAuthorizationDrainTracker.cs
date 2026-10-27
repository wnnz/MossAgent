using MossAgent.App.Browser;
using MossAgent.Application.Authorization;

namespace MossAgent.App.ViewModels;

/// <summary>
/// 将授权监视器的项目排空状态同步到侧栏项目行；事件回调先切回 UI 线程再更新行状态。
/// 行状态取本机与共享排空板聚合的最坏值，读取失败时回退本机状态。
/// </summary>
internal sealed class WorkspaceAuthorizationDrainTracker : IDisposable
{
    private readonly ProjectAuthorizationMonitor _monitor;
    private readonly Func<Guid, WorkspaceProjectNavigationViewModel?> _findRow;
    private readonly Dictionary<Guid, int> _versions = [];
    private int _nextVersion;

    public WorkspaceAuthorizationDrainTracker(ProjectAuthorizationMonitor monitor,
        Func<Guid, WorkspaceProjectNavigationViewModel?> findRow)
    {
        _monitor = monitor;
        _findRow = findRow;
        _monitor.DrainStateChanged += HandleDrainStateChanged;
    }

    public void Refresh(WorkspaceProjectNavigationViewModel row) => _ = RefreshAsync(row);

    public void Dispose() => _monitor.DrainStateChanged -= HandleDrainStateChanged;

    private void HandleDrainStateChanged(Guid projectId) =>
        _ = AvaloniaUiThread.RunAsync(() =>
        {
            if (_findRow(projectId) is { } row) _ = RefreshAsync(row);
        }, CancellationToken.None);

    private async Task RefreshAsync(WorkspaceProjectNavigationViewModel row)
    {
        var projectId = row.Project.Id;
        int version;
        lock (_versions) { version = ++_nextVersion; _versions[projectId] = version; }
        var state = await _monitor.ObserveDrainStateAsync(projectId, CancellationToken.None)
            .ConfigureAwait(false);
        await AvaloniaUiThread.RunAsync(() =>
        {
            lock (_versions) { if (_versions.GetValueOrDefault(projectId) != version) return; }
            row.AuthorizationDrain = state;
        }, CancellationToken.None);
    }
}
