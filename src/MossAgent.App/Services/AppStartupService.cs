using Avalonia.Threading;
using MossAgent.App.ViewModels;
using MossAgent.Application.Git;
using MossAgent.Mcp;

namespace MossAgent.App.Services;

public sealed class AppStartupService : IAsyncDisposable
{
    private readonly IMcpManager _mcp;
    private readonly AppStartupViewModel _state;
    private readonly Action<Action> _publish;
    private readonly IWorktreeCreationIntents? _creationIntents;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _gate = new();
    private Task? _loading;
    private bool _disposed;

    public AppStartupService(IMcpManager mcp, AppStartupViewModel state, Action<Action>? publish = null,
        IWorktreeCreationIntents? creationIntents = null)
    {
        _mcp = mcp;
        _state = state;
        _publish = publish ?? (callback => Dispatcher.UIThread.Post(callback));
        _creationIntents = creationIntents;
    }

    public Task StartAsync()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_loading is not null) return _loading;
            _state.Begin();
            _loading = Task.Run(LoadAsync, _shutdown.Token);
            return _loading;
        }
    }

    private async Task LoadAsync()
    {
        var hasWarning = false;
        var worktreeWarning = false;
        try
        {
            await _mcp.ReloadAsync(_shutdown.Token).ConfigureAwait(false);
            hasWarning = _mcp.Errors.Count > 0;
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
        catch (Exception) { hasWarning = true; }
        if (_creationIntents?.SupportsCreationIntents == true)
        {
            try { await _creationIntents.ReconcileAsync(_shutdown.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
            catch (Exception) { worktreeWarning = true; }
        }
        lock (_gate)
        {
            if (_disposed) return;
            _publish(() =>
            {
                lock (_gate)
                {
                    if (!_disposed) _state.Complete(hasWarning, worktreeWarning);
                }
            });
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? loading;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            loading = _loading;
        }
        await _shutdown.CancelAsync().ConfigureAwait(false);
        if (loading is not null) await loading.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _shutdown.Dispose();
    }
}
