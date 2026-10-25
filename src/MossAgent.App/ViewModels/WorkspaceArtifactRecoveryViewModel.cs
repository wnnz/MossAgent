using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceArtifactRecoveryViewModel : ObservableObject, IDisposable
{
    private readonly IArtifactSessionJournal? _journal;
    private readonly SettingsOperationLifetime _lifetime = new();
    private readonly object _gate = new();
    private readonly List<Task> _operations = [];
    private CancellationTokenSource? _read;
    private ProjectProfile? _project;
    private string? _notice;
    private int _generation;
    private int _busy;
    private bool _disposed;

    public WorkspaceArtifactRecoveryViewModel(IArtifactSessionJournal? journal)
    {
        _journal = journal;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync,
            () => !_disposed && IsAvailable && _project is not null && !IsBusy);
    }

    public ObservableCollection<WorkspaceArtifactRecoveryItemViewModel> Items { get; } = [];
    public bool IsAvailable => _journal is not null;
    public bool HasItems => Items.Count > 0;
    public bool IsVisible => IsAvailable && (HasItems || HasNotice);
    public bool IsBusy => _busy > 0;
    public bool HasNotice => _notice is not null;
    public string Notice => _notice is null ? string.Empty : WorkspaceText.Get(_notice);
    public string Title => string.Format(WorkspaceText.Get("ArtifactRecoveryTitle"), Items.Count);
    public string Hint => WorkspaceText.Get("ArtifactRecoveryHint");
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
            var records = await _journal!.ReadUnresolvedAsync(project, operation.Token).ConfigureAwait(false);
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed || generation != _generation || _project?.Id != project.Id) return;
                Items.Clear();
                foreach (var record in records)
                    Items.Add(new WorkspaceArtifactRecoveryItemViewModel(record, AcknowledgeItemAsync, CanMutate));
                NotifyStateChanged();
            }, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            await PublishNoticeAsync("WorktreeRecoveryCancelled",
                () => generation == _generation && ReferenceEquals(_read, operation));
        }
        catch (Exception)
        {
            await PublishNoticeAsync("ArtifactRecoveryLoadFailed", () => generation == _generation);
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

    private Task AcknowledgeItemAsync(WorkspaceArtifactRecoveryItemViewModel item)
    {
        Task task;
        lock (_gate)
        {
            if (_disposed || _busy > 0 || _journal is not { } journal) return Task.CompletedTask;
            var operation = _lifetime.CreateOperation(CancellationToken.None);
            task = TrackLocked(AcknowledgeAsync(item, operation, journal));
        }
        NotifyStateChanged();
        return task;
    }

    private async Task AcknowledgeAsync(WorkspaceArtifactRecoveryItemViewModel item,
        CancellationTokenSource operation, IArtifactSessionJournal journal)
    {
        try
        {
            await journal.AcknowledgeAsync(item.RecordId, operation.Token).ConfigureAwait(false);
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed) return;
                Items.Remove(item);
                _notice = null;
                NotifyStateChanged();
            }, CancellationToken.None);
        }
        catch (ArtifactSessionStaleException)
        {
            await PublishNoticeAsync("WorktreeRecoveryStale");
            await RefreshAsync();
        }
        catch (OperationCanceledException)
        {
            await PublishNoticeAsync("WorktreeRecoveryCancelled");
        }
        catch (Exception)
        {
            await PublishNoticeAsync("WorktreeRecoveryAcknowledgeFailed");
        }
        finally
        {
            await AvaloniaUiThread.RunAsync(EndOperation, CancellationToken.None);
            operation.Dispose();
        }
    }

    private Task PublishNoticeAsync(string key, Func<bool>? guard = null) =>
        AvaloniaUiThread.RunAsync(() =>
        {
            if (_disposed || guard?.Invoke() == false) return;
            _notice = key;
            NotifyStateChanged();
        }, CancellationToken.None);

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
