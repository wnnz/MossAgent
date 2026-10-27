using MossAgent.App.Browser;
using MossAgent.Application.Authorization;

namespace MossAgent.App.ViewModels;

/// <summary>
/// 将授权监视器的项目排空状态同步到侧栏项目行；事件回调先切回 UI 线程再更新行状态。
/// 行状态取本机与共享排空板聚合的最坏值，读取失败时回退本机状态。板上不推送事件，
/// 故观察结果为非 Idle 的项目行每约 30 秒安排一次延迟再观察（每项目至多一个在途、
/// 解析为 Idle 即停、随跟踪器释放取消），外部实例的行变化由此收敛到徽标。
/// </summary>
internal sealed class WorkspaceAuthorizationDrainTracker : IDisposable
{
    private static readonly TimeSpan ReobserveInterval = TimeSpan.FromSeconds(30);

    private readonly ProjectAuthorizationMonitor _monitor;
    private readonly Func<Guid, WorkspaceProjectNavigationViewModel?> _findRow;
    private readonly TimeProvider _time;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<Guid, int> _versions = [];
    private readonly HashSet<Guid> _reobservePending = [];
    private int _nextVersion;
    private bool _disposed;

    public WorkspaceAuthorizationDrainTracker(ProjectAuthorizationMonitor monitor,
        Func<Guid, WorkspaceProjectNavigationViewModel?> findRow, TimeProvider? timeProvider = null)
    {
        _monitor = monitor;
        _findRow = findRow;
        _time = timeProvider ?? TimeProvider.System;
        _monitor.DrainStateChanged += HandleDrainStateChanged;
    }

    public void Refresh(WorkspaceProjectNavigationViewModel row) => _ = RefreshAsync(row);

    public void Dispose()
    {
        _monitor.DrainStateChanged -= HandleDrainStateChanged;
        lock (_versions) _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

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
        TrackReobserve(projectId, version, state);
        await AvaloniaUiThread.RunAsync(() =>
        {
            lock (_versions) { if (_versions.GetValueOrDefault(projectId) != version) return; }
            row.AuthorizationDrain = state;
        }, CancellationToken.None);
    }

    /// <summary>仅当本次刷新仍是该项目最新观察时更新再观察簿记：解析为 Idle 即作废
    /// 在途延迟检查，非 Idle 且尚无在途检查时才补排一次，保证每项目至多一个待触发。</summary>
    private void TrackReobserve(Guid projectId, int version, AuthorizationDrainState state)
    {
        lock (_versions)
        {
            if (_disposed || _versions.GetValueOrDefault(projectId) != version) return;
            if (state == AuthorizationDrainState.Idle)
            {
                _reobservePending.Remove(projectId);
                return;
            }
            if (!_reobservePending.Add(projectId)) return;
        }
        _ = ReobserveAfterDelayAsync(projectId);
    }

    private async Task ReobserveAfterDelayAsync(Guid projectId)
    {
        try
        {
            await Task.Delay(ReobserveInterval, _time, _lifetime.Token).ConfigureAwait(false);
            lock (_versions)
            {
                if (_disposed || !_reobservePending.Remove(projectId)) return;
            }
            if (_findRow(projectId) is { } row) await RefreshAsync(row).ConfigureAwait(false);
        }
        catch { /* 徽标再观察是尽力而为：取消、释放与读取失败都静默收尾。 */ }
    }
}
