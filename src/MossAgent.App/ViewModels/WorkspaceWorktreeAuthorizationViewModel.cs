using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Authorization;
using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceWorktreeAuthorizationViewModel(
    WorkspaceProjectService projects) : ObservableObject, IDisposable
{
    private ProjectProfile? _project;
    private AgentTask? _task;
    private CancellationTokenSource? _operation;
    private Task _pending = Task.CompletedTask;
    private bool _available;
    private bool _isBusy;
    private bool _hasGrant;
    private bool _requiresAuthorizationAction;
    private bool _isLoaded;
    private bool _disposed;
    private bool _reauthorizationConfirmationPending;
    private Guid? _confirmationTaskId;
    private string _status = string.Empty;
    private IAsyncRelayCommand? _confirm;
    private IAsyncRelayCommand? _reauthorize;
    private IAsyncRelayCommand? _confirmReauthorize;
    private IRelayCommand? _retry;

    public event Action<ProjectProfile>? ProjectChanged;

    public string Path => _task?.WorktreePath ?? string.Empty;
    public bool IsVisible => projects.SupportsWorktreeAuthorization
        && _task?.WorktreePath is not null;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) NotifyCommands(); } }
    public bool IsReauthorizationConfirmationPending
    {
        get => _reauthorizationConfirmationPending;
        private set
        {
            if (!SetProperty(ref _reauthorizationConfirmationPending, value)) return;
            OnPropertyChanged(nameof(ReauthorizeLabel));
            NotifyCommands();
        }
    }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string ConfirmLabel => WorkspaceText.Get("WorktreeObjectConfirm");
    public string ReauthorizeLabel => WorkspaceText.Get("WorktreeObjectReauthorize");
    public string ConfirmReauthorizeLabel =>
        WorkspaceText.Get("WorktreeObjectReauthorizeConfirm");
    public string RetryLabel => WorkspaceText.Get("WorktreeObjectRefresh");
    public IAsyncRelayCommand ConfirmCommand =>
        _confirm ??= new AsyncRelayCommand(ConfirmAsync, CanConfirm);
    public IAsyncRelayCommand ReauthorizeCommand =>
        _reauthorize ??= new AsyncRelayCommand(ReauthorizeAsync, CanReauthorize);
    public IAsyncRelayCommand ConfirmReauthorizeCommand =>
        _confirmReauthorize ??= new AsyncRelayCommand(
            ConfirmReauthorizationAsync, CanConfirmReauthorization);
    public IRelayCommand RefreshCommand =>
        _retry ??= new RelayCommand(Refresh, () => !_disposed && !IsBusy && IsVisible);

    public bool BlocksTaskStart(Guid taskId) =>
        _confirmationTaskId == taskId
        || _isLoaded && _task?.Id == taskId && _requiresAuthorizationAction;

    internal void SetContext(ProjectProfile? project, AgentTask? task, bool available)
    {
        _available = available;
        if (_project == project && _task == task) { NotifyCommands(); return; }
        _operation?.Cancel();
        _project = project;
        _task = task;
        _hasGrant = false;
        _requiresAuthorizationAction = false;
        _isLoaded = false;
        IsReauthorizationConfirmationPending = false;
        OnPropertyChanged(nameof(Path));
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(ReauthorizeLabel));
        if (IsVisible && !IsBusy) Refresh();
        NotifyCommands();
    }

    private bool CanConfirm() => !_disposed && _available && IsVisible
        && _isLoaded && !_hasGrant && !IsBusy;

    private bool CanReauthorize() => !_disposed && _available && IsVisible
        && _isLoaded && _hasGrant && _requiresAuthorizationAction && !IsBusy
        && !IsReauthorizationConfirmationPending;

    private bool CanConfirmReauthorization() => !_disposed && _available && IsVisible
        && _isLoaded && _hasGrant && _requiresAuthorizationAction && !IsBusy
        && IsReauthorizationConfirmationPending;

    private void Refresh()
    {
        if (_disposed || IsBusy || _task is null) return;
        _pending = RunAsync(checkOnly: true, confirmLegacy: false, reauthorize: false);
    }

    private Task ConfirmAsync()
    {
        if (!CanConfirm()) return Task.CompletedTask;
        return _pending = RunAsync(checkOnly: false, confirmLegacy: true, reauthorize: false);
    }

    private Task ReauthorizeAsync()
    {
        if (!CanReauthorize()) return Task.CompletedTask;
        IsReauthorizationConfirmationPending = true;
        Status = WorkspaceText.Get("WorktreeObjectReauthorizeNotice");
        return Task.CompletedTask;
    }

    private Task ConfirmReauthorizationAsync()
    {
        if (!IsReauthorizationConfirmationPending)
            return Task.CompletedTask;
        IsReauthorizationConfirmationPending = false;
        return _pending = RunAsync(checkOnly: false, confirmLegacy: false, reauthorize: true);
    }

    private async Task RunAsync(bool checkOnly, bool confirmLegacy, bool reauthorize)
    {
        var project = _project;
        var task = _task;
        if (project is null || task is null || (!checkOnly && !confirmLegacy && !reauthorize)) return;
        using var operation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _operation = operation;
        _confirmationTaskId = checkOnly ? null : task.Id;
        IsBusy = true;
        Status = WorkspaceText.Get("WorktreeObjectChecking");
        try
        {
            if (confirmLegacy)
                await projects.ConfirmLegacyWorktreeAsync(project, task, operation.Token);
            if (reauthorize)
                project = await ReauthorizeAndPublishAsync(project, task, operation.Token);
            await RefreshAuthorizationStateAsync(project, task, operation.Token);
        }
        catch (ProjectAuthorizationNotQuiescedException failure)
        {
            ProjectChanged?.Invoke(failure.SavedProject);
            if (!_disposed && _task == task)
            {
                _hasGrant = true;
                _requiresAuthorizationAction = true;
                _isLoaded = true;
                Status = WorkspaceText.Get("WorktreeObjectDrainPending");
            }
        }
        catch (OperationCanceledException)
        {
            if (!_disposed && _task == task)
                Status = WorkspaceText.Get("WorktreeObjectCancelled");
        }
        catch (Exception)
        {
            if (!_disposed && _task == task)
                Status = WorkspaceText.Get("WorktreeObjectFailure");
        }
        finally
        {
            _operation = null;
            _confirmationTaskId = null;
            IsBusy = false;
            if (!_disposed && (_task != task || _project != project) && IsVisible)
                Refresh();
        }
    }

    private async Task<ProjectProfile> ReauthorizeAndPublishAsync(
        ProjectProfile project, AgentTask task, CancellationToken cancellationToken)
    {
        var updated = await projects.ReauthorizeWorktreeAsync(
            project, task, cancellationToken);
        if (_task == task && !_disposed) _project = updated;
        ProjectChanged?.Invoke(updated);
        return updated;
    }

    private async Task RefreshAuthorizationStateAsync(
        ProjectProfile project, AgentTask task, CancellationToken cancellationToken)
    {
        var grant = await projects.ReadWorktreeAuthorizationAsync(
            task.Id, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (_project != project || _task != task || _disposed) return;
        _hasGrant = grant is not null;
        _requiresAuthorizationAction = false;
        _isLoaded = true;
        if (grant is null)
        {
            _requiresAuthorizationAction = true;
            Status = WorkspaceText.Get("WorktreeObjectLegacyNotice");
            return;
        }
        try
        {
            await projects.ValidateWorktreeAsync(
                project, task.Id, task.WorktreePath!, cancellationToken);
            _requiresAuthorizationAction = false;
            Status = WorkspaceText.Get("WorktreeObjectEnrolled");
        }
        catch (WorkspaceAuthorizationException)
        {
            throw;
        }
        catch (WorktreeAuthorizationException)
        {
            _requiresAuthorizationAction = true;
            Status = WorkspaceText.Get("WorktreeObjectChanged");
        }
        catch (UnauthorizedAccessException)
        {
            _requiresAuthorizationAction = true;
            Status = WorkspaceText.Get("WorktreeObjectChanged");
        }
    }

    private void NotifyCommands()
    {
        _confirm?.NotifyCanExecuteChanged();
        _reauthorize?.NotifyCanExecuteChanged();
        _confirmReauthorize?.NotifyCanExecuteChanged();
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
