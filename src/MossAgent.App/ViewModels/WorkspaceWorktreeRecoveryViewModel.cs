using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceWorktreeRecoveryViewModel : ObservableObject, IDisposable
{
    private readonly IWorktreeCreationIntents? _intents;
    private readonly IWorktreeCleanupIntents? _cleanupIntents;
    private readonly SettingsOperationLifetime _lifetime = new();
    private readonly object _gate = new();
    private readonly List<Task> _operations = [];
    private CancellationTokenSource? _read;
    private ProjectProfile? _project;
    private string? _notice;
    private int _generation;
    private int _busy;
    private bool _disposed;

    public WorkspaceWorktreeRecoveryViewModel(IWorktreeCreationIntents? intents,
        IWorktreeCleanupIntents? cleanupIntents = null)
    {
        _intents = intents;
        _cleanupIntents = cleanupIntents;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync,
            () => !_disposed && IsAvailable && _project is not null && !IsBusy);
    }

    public ObservableCollection<WorkspaceWorktreeRecoveryItemViewModel> Items { get; } = [];
    public bool IsAvailable => _intents?.SupportsCreationIntents == true
        || _cleanupIntents?.SupportsCleanupIntents == true;
    public bool HasItems => Items.Count > 0;
    public bool IsVisible => IsAvailable && (HasItems || HasNotice);
    public bool IsBusy => _busy > 0;
    public bool HasNotice => _notice is not null;
    public string Notice => _notice is null ? string.Empty : WorkspaceText.Get(_notice);
    public string Title => string.Format(WorkspaceText.Get("WorktreeRecoveryTitle"), Items.Count);
    public string Hint => WorkspaceText.Get("WorktreeRecoveryHint");
    public string RefreshLabel => WorkspaceText.Get("WorktreeRecoveryRefresh");
    public IAsyncRelayCommand RefreshCommand { get; }

    internal void SetProject(ProjectProfile? project)
    {
        lock (_gate)
        {
            if (_disposed) return;
            var changed = _project?.Id != project?.Id;
            _project = project;
            if (!changed) return;
            _generation++;
            try { _read?.Cancel(); _read = null; }
            catch (ObjectDisposedException) { }
            Items.Clear();
            _notice = null;
        }
        NotifyStateChanged();
        if (project is not null) _ = RefreshAsync();
    }

    public Task RefreshAsync()
    {
        Task task;
        lock (_gate)
        {
            if (_disposed || !IsAvailable || _project is not { } project) return Task.CompletedTask;
            try { _read?.Cancel(); } catch (ObjectDisposedException) { }
            var operation = _lifetime.CreateOperation(CancellationToken.None);
            _read = operation;
            task = TrackLocked(ReadAsync(_generation, project, operation));
        }
        NotifyStateChanged();
        return task;
    }

    private Task TrackLocked(Task operation)
    {
        _operations.Add(operation);
        Interlocked.Increment(ref _busy);
        _ = operation.ContinueWith(completed =>
        {
            lock (_gate) _operations.Remove(completed);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return operation;
    }

    private async Task ReadAsync(int generation, ProjectProfile project, CancellationTokenSource operation)
    {
        try
        {
            var creation = _intents?.SupportsCreationIntents == true
                ? await _intents.ReadIncompleteAsync(project, operation.Token).ConfigureAwait(false)
                : (IReadOnlyList<WorktreeCreationIntent>)[];
            var cleanup = _cleanupIntents?.SupportsCleanupIntents == true
                ? await _cleanupIntents.ReadIncompleteAsync(project, operation.Token).ConfigureAwait(false)
                : (IReadOnlyList<WorktreeCleanupIntent>)[];
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed || generation != _generation || _project?.Id != project.Id) return;
                Items.Clear();
                foreach (var intent in creation)
                    Items.Add(new WorkspaceWorktreeRecoveryItemViewModel(intent, AcknowledgeItemAsync, CanMutate));
                foreach (var intent in cleanup)
                    Items.Add(new WorkspaceWorktreeRecoveryItemViewModel(intent, AcknowledgeItemAsync, CanMutate));
                NotifyStateChanged();
            }, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed || generation != _generation || !ReferenceEquals(_read, operation)) return;
                _notice = "WorktreeRecoveryCancelled";
                NotifyStateChanged();
            }, CancellationToken.None);
        }
        catch (Exception)
        {
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed || generation != _generation) return;
                _notice = "WorktreeRecoveryLoadFailed";
                NotifyStateChanged();
            }, CancellationToken.None);
        }
        finally
        {
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (ReferenceEquals(_read, operation)) _read = null;
                EndOperation();
            }, CancellationToken.None);
            operation.Dispose();
        }
    }

    private Task AcknowledgeItemAsync(WorkspaceWorktreeRecoveryItemViewModel item)
    {
        Task task;
        lock (_gate)
        {
            if (_disposed || _busy > 0 || Port(item) is not { } port) return Task.CompletedTask;
            var operation = _lifetime.CreateOperation(CancellationToken.None);
            task = TrackLocked(AcknowledgeAsync(item, operation, port));
        }
        NotifyStateChanged();
        return task;
    }

    private Func<Guid, CancellationToken, Task>? Port(WorkspaceWorktreeRecoveryItemViewModel item) =>
        item.IsCleanupItem
            ? _cleanupIntents is null ? null : _cleanupIntents.AcknowledgeAsync
            : _intents is null ? null : _intents.AcknowledgeAsync;

    private async Task AcknowledgeAsync(WorkspaceWorktreeRecoveryItemViewModel item,
        CancellationTokenSource operation, Func<Guid, CancellationToken, Task> port)
    {
        try
        {
            await port(item.TaskId, operation.Token).ConfigureAwait(false);
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed) return;
                Items.Remove(item);
                _notice = null;
                NotifyStateChanged();
            }, CancellationToken.None);
        }
        catch (WorktreeAuthorizationException)
        {
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed) return;
                _notice = "WorktreeRecoveryStale";
                NotifyStateChanged();
            }, CancellationToken.None);
            await RefreshAsync();
        }
        catch (OperationCanceledException)
        {
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed) return;
                _notice = "WorktreeRecoveryCancelled";
                NotifyStateChanged();
            }, CancellationToken.None);
        }
        catch (Exception)
        {
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed) return;
                _notice = "WorktreeRecoveryAcknowledgeFailed";
                NotifyStateChanged();
            }, CancellationToken.None);
        }
        finally
        {
            await AvaloniaUiThread.RunAsync(EndOperation, CancellationToken.None);
            operation.Dispose();
        }
    }

    private void EndOperation()
    {
        Interlocked.Decrement(ref _busy);
        NotifyStateChanged();
    }

    private bool CanMutate() => !_disposed && !IsBusy && IsAvailable;

    private void NotifyStateChanged()
    {
        if (_disposed) return;
        foreach (var name in new[] { nameof(HasItems), nameof(IsVisible), nameof(IsBusy), nameof(HasNotice), nameof(Notice), nameof(Title) })
            OnPropertyChanged(name);
        RefreshCommand.NotifyCanExecuteChanged();
        foreach (var item in Items) item.NotifyCommands();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        try { _read?.Cancel(); } catch (ObjectDisposedException) { }
        _lifetime.Dispose();
    }

    public async Task StopAsync()
    {
        Dispose();
        Task[] pending;
        lock (_gate) pending = _operations.ToArray();
        await Task.WhenAll(pending).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }
}
