using Avalonia.Threading;
using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using MossAgent.Application.Git;
using MossAgent.Application.Persistence;
using MossAgent.Application.Plugins;
using MossAgent.Mcp;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.Services;

public sealed class AppStartupService : IAsyncDisposable
{
    private readonly IMcpManager _mcp;
    private readonly AppStartupViewModel _state;
    private readonly Action<Action> _publish;
    private readonly IWorktreeCreationIntents? _creationIntents;
    private readonly IAttachmentSnapshotReaper? _reaper;
    private readonly IArtifactSessionJournal? _artifactJournal;
    private readonly ISubagentRunJournal? _runJournal;
    private readonly IBrowserSessionJournal? _browserJournal;
    private readonly IPluginPackageInstaller? _pluginInstaller;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _gate = new();
    private Task? _loading;
    private bool _disposed;

    public AppStartupService(IMcpManager mcp, AppStartupViewModel state, Action<Action>? publish = null,
        IWorktreeCreationIntents? creationIntents = null, IAttachmentSnapshotReaper? reaper = null,
        IArtifactSessionJournal? artifactJournal = null, ISubagentRunJournal? runJournal = null,
        IBrowserSessionJournal? browserJournal = null, IPluginPackageInstaller? pluginInstaller = null)
    {
        _mcp = mcp;
        _state = state;
        _publish = publish ?? (callback => Dispatcher.UIThread.Post(callback));
        _creationIntents = creationIntents;
        _reaper = reaper;
        _artifactJournal = artifactJournal;
        _runJournal = runJournal;
        _browserJournal = browserJournal;
        _pluginInstaller = pluginInstaller;
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
        var storageWarning = false;
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
        if (_reaper is not null)
        {
            try { storageWarning = (await _reaper.ReclaimAsync(_shutdown.Token).ConfigureAwait(false)).HasFailures; }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
            catch (Exception) { storageWarning = true; }
        }
        if (_artifactJournal is not null)
        {
            try { await _artifactJournal.ReconcileAsync(_shutdown.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
            catch (Exception) { storageWarning = true; }
        }
        // Marks leftover running subagent rows interrupted only; never replays a run.
        if (_runJournal is not null)
        {
            try { await _runJournal.MarkInterruptedRunningAsync(_shutdown.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
            catch (Exception) { storageWarning = true; }
        }
        // Marks leftover attached sessions and committed browser intents interrupted
        // only; a journaled browser side effect is never replayed automatically.
        if (_browserJournal is not null)
        {
            try { await _browserJournal.ReconcileAsync(_shutdown.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
            catch (Exception) { storageWarning = true; }
        }
        // Clears interrupted-install staging trees and reports payloads without a row;
        // orphaned finals are never deleted or executed automatically.
        if (_pluginInstaller is not null)
        {
            try
            {
                var result = await _pluginInstaller.ReconcileAsync(_shutdown.Token).ConfigureAwait(false);
                storageWarning |= result.OrphanedFinals.Count > 0;
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
            catch (Exception) { storageWarning = true; }
        }
        lock (_gate)
        {
            if (_disposed) return;
            _publish(() =>
            {
                lock (_gate)
                {
                    if (!_disposed) _state.Complete(hasWarning, worktreeWarning, storageWarning);
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
