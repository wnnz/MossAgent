using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceWorktreeAuthorizationViewModel(IGitWorktreeAuthorizationService? service) : ObservableObject, IDisposable
{
    private ProjectProfile? _project;
    private AgentTask? _task;
    private CancellationTokenSource? _operation;
    private Task _pending = Task.CompletedTask;
    private bool _available;
    private bool _isBusy;
    private bool _hasGrant;
    private bool _replaced;
    private bool _isLoaded;
    private bool _disposed;
    private Guid? _confirmationTaskId;
    private string _status = string.Empty;
    private IAsyncRelayCommand? _confirm;
    private IRelayCommand? _retry;

    public string Path => _task?.WorktreePath ?? string.Empty;
    public bool IsVisible => service is not null && _task?.WorktreePath is not null;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) NotifyCommands(); } }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string ConfirmLabel => WorkspaceText.Get(_replaced ? "WorktreeObjectReauthorize" : "WorktreeObjectConfirm");
    public string RetryLabel => WorkspaceText.Get("WorktreeObjectRefresh");
    public IAsyncRelayCommand ConfirmCommand => _confirm ??= new AsyncRelayCommand(ConfirmAsync, CanConfirm);
    public IRelayCommand RefreshCommand => _retry ??= new RelayCommand(Refresh, () => !_disposed && !IsBusy && IsVisible);
    public bool BlocksTaskStart(Guid taskId) => _confirmationTaskId == taskId;

    internal void SetContext(ProjectProfile? project, AgentTask? task, bool available)
    {
        _available = available;
        if (_project == project && _task == task) { NotifyCommands(); return; }
        _operation?.Cancel();
        _project = project;
        _task = task;
        _hasGrant = false;
        _replaced = false;
        _isLoaded = false;
        OnPropertyChanged(nameof(ConfirmLabel));
        OnPropertyChanged(nameof(Path));
        OnPropertyChanged(nameof(IsVisible));
        if (IsVisible && !IsBusy) Refresh();
        NotifyCommands();
    }

    private bool CanConfirm() => !_disposed && _available && IsVisible && _isLoaded && (!_hasGrant || _replaced) && !IsBusy;

    private void Refresh()
    {
        if (_disposed || IsBusy || service is null || _task is null) return;
        _pending = RunAsync(false);
    }

    private Task ConfirmAsync()
    {
        if (!CanConfirm()) return Task.CompletedTask;
        return _pending = RunAsync(true);
    }

    private async Task RunAsync(bool confirm)
    {
        var project = _project;
        var task = _task;
        if (project is null || task is null || service is null) return;
        using var operation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _operation = operation;
        _confirmationTaskId = confirm ? task.Id : null;
        IsBusy = true;
        Status = WorkspaceText.Get("WorktreeObjectChecking");
        try
        {
            // The confirm choice reuses the state captured by this refresh; the persisted
            // compare-and-swap makes a stale decision fail closed instead of overwriting.
            if (confirm)
            {
                if (_hasGrant && _replaced) await service.ReauthorizeReplacedAsync(project, task, operation.Token);
                else await service.ConfirmLegacyAsync(project, task, operation.Token);
            }
            var grant = await service.ReadAuthorizationAsync(task.Id, operation.Token);
            var state = grant is null ? WorktreeEnrollmentState.Unconfirmed
                : await ProbeAuthorizationAsync(project, task, operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (_project != project || _task != task || _disposed) return;
            _hasGrant = grant is not null;
            _replaced = state == WorktreeEnrollmentState.Replaced;
            _isLoaded = true;
            OnPropertyChanged(nameof(ConfirmLabel));
            Status = WorkspaceText.Get(_replaced ? "WorktreeObjectReplaced"
                : _hasGrant ? "WorktreeObjectEnrolled" : "WorktreeObjectLegacyNotice");
        }
        catch (OperationCanceledException) { if (!_disposed && _task == task) Status = WorkspaceText.Get("WorktreeObjectCancelled"); }
        catch (Exception) { if (!_disposed && _task == task) Status = WorkspaceText.Get("WorktreeObjectFailure"); }
        finally
        {
            _operation = null;
            _confirmationTaskId = null;
            IsBusy = false;
            if (!_disposed && (_task != task || _project != project) && IsVisible) Refresh();
        }
    }

    private async Task<WorktreeEnrollmentState> ProbeAuthorizationAsync(ProjectProfile project, AgentTask task, CancellationToken token)
    {
        try { return await service!.ProbeAuthorizationAsync(project, task, token); }
        catch { return WorktreeEnrollmentState.Unverifiable; }
    }

    private void NotifyCommands()
    {
        _confirm?.NotifyCanExecuteChanged();
        _retry?.NotifyCanExecuteChanged();
    }

    public async Task StopAsync() { Dispose(); await _pending; }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _operation?.Cancel();
        NotifyCommands();
    }
}
