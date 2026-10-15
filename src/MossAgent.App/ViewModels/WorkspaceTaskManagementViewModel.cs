using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceTaskManagementViewModel : ObservableObject, IDisposable
{
    private readonly IWorkspaceRepository _workspaces;
    private readonly SettingsMutationRunner _mutations = new();
    private readonly SettingsOperationLifetime _operations;
    private AgentTask? _selectedTask;
    private AgentTask? _selectedArchivedTask;
    private AgentTask? _editingTask;
    private Guid? _loadedProjectId;
    private Guid? _blockedTaskId;
    private string _editingTitle = string.Empty;
    private string _status = string.Empty;
    private bool _isEditing;
    private bool _isArchiveVisible;
    private int _loadVersion;
    private int _pinLoadVersion;
    private int _editRevision;
    private bool _hasStatusError;
    private bool _disposed;

    public WorkspaceTaskManagementViewModel(IWorkspaceRepository workspaces, TimeSpan? mutationTimeout = null)
    {
        _workspaces = workspaces;
        _operations = new SettingsOperationLifetime(mutationTimeout);
        BeginRenameCommand = new RelayCommand<AgentTask>(BeginRename, task => CanModifyTask(task ?? SelectedTask));
        SaveRenameCommand = new AsyncRelayCommand(SaveRenameAsync, CanSaveRename);
        CancelRenameCommand = new RelayCommand(CancelRename);
        ArchiveCommand = new AsyncRelayCommand<AgentTask>(ArchiveAsync,
            task => CanWrite && CanModifyTask(task ?? SelectedTask));
        TogglePinCommand = new AsyncRelayCommand<AgentTask>(TogglePinAsync,
            task => CanWrite && (task ?? SelectedTask) is not null);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync, () => CanWrite && SelectedArchivedTask is not null);
        ToggleArchiveCommand = new RelayCommand(ToggleArchive);
        _mutations.PropertyChanged += HandleMutationChanged;
    }

    public event Action<AgentTask>? TaskRenamed;
    public event Action<AgentTask>? TaskArchived;
    public event Action<AgentTask>? TaskRestored;
    public event Action<AgentTask>? TaskPinChanged;
    public Func<Guid, bool>? IsTaskRunning { get; set; }
    public ObservableCollection<AgentTask> ArchivedTasks { get; } = [];
    public WorkspacePinnedTasksViewModel PinnedTasks { get; } = new();
    public IRelayCommand<AgentTask> BeginRenameCommand { get; }
    public IAsyncRelayCommand SaveRenameCommand { get; }
    public IRelayCommand CancelRenameCommand { get; }
    public IAsyncRelayCommand<AgentTask> ArchiveCommand { get; }
    public IAsyncRelayCommand<AgentTask> TogglePinCommand { get; }
    public IAsyncRelayCommand RestoreCommand { get; }
    public IRelayCommand ToggleArchiveCommand { get; }

    public AgentTask? SelectedTask
    {
        get => _selectedTask;
        set
        {
            var previousId = _selectedTask?.Id;
            if (!SetProperty(ref _selectedTask, value)) return;
            if (previousId != value?.Id) CancelRename();
            else if (value is not null && _editingTask?.Id == value.Id) _editingTask = value;
            PinnedTasks.SetSelectedTask(value?.Id);
            OnPropertyChanged(nameof(PinToggleLabel));
            NotifyCommands();
        }
    }

    public AgentTask? SelectedArchivedTask
    {
        get => _selectedArchivedTask;
        set
        {
            if (SetProperty(ref _selectedArchivedTask, value))
                RestoreCommand.NotifyCanExecuteChanged();
        }
    }

    public string EditingTitle
    {
        get => _editingTitle;
        set
        {
            if (SetProperty(ref _editingTitle, value))
            {
                _editRevision++;
                SaveRenameCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool HasStatusError { get => _hasStatusError; private set => SetProperty(ref _hasStatusError, value); }
    public bool IsBusy => _mutations.IsRunning;
    public bool BlocksTaskStart(Guid taskId) => IsBusy && _blockedTaskId == taskId;
    public bool IsEditing { get => _isEditing; private set => SetProperty(ref _isEditing, value); }
    public bool IsArchiveVisible
    {
        get => _isArchiveVisible;
        private set
        {
            if (SetProperty(ref _isArchiveVisible, value))
                OnPropertyChanged(nameof(ArchiveToggleLabel));
        }
    }
    public string ArchiveToggleLabel => string.Format(CultureInfo.CurrentCulture,
        SettingsText.Get(IsArchiveVisible ? "TaskArchiveHide" : "TaskArchiveCount"), ArchivedTasks.Count);
    public bool HasArchivedTasks => ArchivedTasks.Count > 0;
    public string PinToggleLabel => SettingsText.Get(SelectedTask?.IsPinned == true ? "UnpinTask" : "PinTask");

    public async Task LoadPinnedAsync(string query)
    {
        if (_disposed) return;
        var version = Interlocked.Increment(ref _pinLoadVersion);
        var tasks = await _workspaces.GetPinnedTasksAsync(CancellationToken.None);
        if (!_disposed && version == _pinLoadVersion) PinnedTasks.SetTasks(tasks, query);
    }

    public async Task LoadAsync(Guid? projectId)
    {
        if (_disposed) return;
        var version = Interlocked.Increment(ref _loadVersion);
        _loadedProjectId = projectId;
        ArchivedTasks.Clear();
        SelectedArchivedTask = null;
        OnPropertyChanged(nameof(ArchiveToggleLabel));
        OnPropertyChanged(nameof(HasArchivedTasks));
        var tasks = await _workspaces.GetArchivedTasksAsync(projectId, CancellationToken.None);
        if (_disposed || version != _loadVersion) return;
        ArchivedTasks.ReplaceWith(tasks);
        OnPropertyChanged(nameof(ArchiveToggleLabel));
        OnPropertyChanged(nameof(HasArchivedTasks));
    }

    public void NotifyTaskRunStateChanged() => NotifyCommands();

    private async Task TogglePinAsync(AgentTask? target, CancellationToken cancellationToken)
    {
        if (!CanWrite || (target ?? SelectedTask) is not { } task) return;
        var isPinned = !task.IsPinned;
        var persisted = false;
        await MutateAsync(async token =>
            persisted = await _workspaces.SetTaskPinnedAsync(task.Id, isPinned, token),
            cancellationToken, "TaskPinFailed", () =>
        {
            if (!persisted)
            {
                ReportFailure("TaskPinUnavailable");
                return;
            }
            var updated = task with { IsPinned = isPinned };
            if (_editingTask?.Id == task.Id) _editingTask = updated;
            if (SelectedTask?.Id == task.Id) SelectedTask = updated;
            PinnedTasks.Upsert(updated);
            TaskPinChanged?.Invoke(updated);
            Status = SettingsText.Get(isPinned ? "TaskPinSucceeded" : "TaskUnpinSucceeded");
        });
    }

    private void BeginRename(AgentTask? target)
    {
        var task = target ?? SelectedTask;
        if (!CanModifyTask(task)) return;
        _editRevision++;
        _editingTask = task;
        EditingTitle = task!.Title;
        IsEditing = true;
        SaveRenameCommand.NotifyCanExecuteChanged();
    }

    private async Task SaveRenameAsync(CancellationToken cancellationToken)
    {
        if (!CanSaveRename() || _editingTask is not { } task) return;
        var revision = _editRevision;
        var title = EditingTitle.Trim();
        AgentTask? updated = null;
        await MutateAsync(async token => updated = await _workspaces.RenameTaskAsync(task, title, token),
            cancellationToken, "TaskRenameFailed", () =>
        {
            if (updated is null)
            {
                ReportFailure("TaskRenameConflict");
                return;
            }
            if (SelectedTask?.Id == task.Id) SelectedTask = updated;
            if (_editingTask?.Id == task.Id)
            {
                if (_editRevision == revision) CancelRename();
                else _editingTask = updated;
            }
            PinnedTasks.Upsert(updated);
            TaskRenamed?.Invoke(updated);
            Status = SettingsText.Get(IsEditing && _editingTask?.Id == task.Id
                ? "TaskPreviousDraftSaved" : "TaskRenameSucceeded");
        }, task.Id);
    }

    private void CancelRename()
    {
        _editRevision++;
        _editingTask = null;
        IsEditing = false;
        EditingTitle = string.Empty;
        SaveRenameCommand.NotifyCanExecuteChanged();
    }

    private async Task ArchiveAsync(AgentTask? target, CancellationToken cancellationToken)
    {
        var task = target ?? SelectedTask;
        if (!CanWrite || !CanModifyTask(task) || task is null) return;
        await MutateAsync(token => _workspaces.SetTaskArchivedAsync(task.Id, true, token),
            cancellationToken, "TaskArchiveFailed", () =>
        {
            if (_editingTask?.Id == task.Id) CancelRename();
            if (_loadedProjectId == task.ProjectId)
                ArchivedTasks.Insert(0, task);
            PinnedTasks.Remove(task.Id);
            OnPropertyChanged(nameof(ArchiveToggleLabel));
            OnPropertyChanged(nameof(HasArchivedTasks));
            TaskArchived?.Invoke(task);
            Status = SettingsText.Get("TaskArchiveSucceeded");
        }, task.Id);
    }

    private async Task RestoreAsync(CancellationToken cancellationToken)
    {
        if (!CanWrite || SelectedArchivedTask is not { } task) return;
        await MutateAsync(token => _workspaces.SetTaskArchivedAsync(task.Id, false, token),
            cancellationToken, "TaskRestoreFailed", () =>
        {
            ArchivedTasks.Remove(task);
            PinnedTasks.Upsert(task);
            if (SelectedArchivedTask?.Id == task.Id) SelectedArchivedTask = null;
            OnPropertyChanged(nameof(ArchiveToggleLabel));
            OnPropertyChanged(nameof(HasArchivedTasks));
            TaskRestored?.Invoke(task);
            Status = SettingsText.Get("TaskRestoreSucceeded");
        });
    }

    private bool CanWrite => !_disposed && !IsBusy;
    private void ToggleArchive() => IsArchiveVisible = !IsArchiveVisible;
    private bool CanModifyTask(AgentTask? task) =>
        !_disposed && task is not null && IsTaskRunning?.Invoke(task.Id) != true;
    private bool CanSaveRename() =>
        CanWrite && IsEditing && CanModifyTask(_editingTask) && !string.IsNullOrWhiteSpace(EditingTitle);

    private async Task MutateAsync(Func<CancellationToken, Task> mutation,
        CancellationToken cancellationToken, string failureKey, Action publish, Guid? blockedTaskId = null)
    {
        if (!CanWrite) return;
        using var operation = _operations.CreateOperation(cancellationToken);
        HasStatusError = false;
        Status = string.Empty;
        _blockedTaskId = blockedTaskId;
        try
        {
            var completed = await _mutations.TryExecuteAsync(async token =>
            {
                await mutation(token);
                token.ThrowIfCancellationRequested();
                if (!_disposed) publish();
            }, operation.Token, () => ReportFailure(failureKey));
            if (!completed && !_disposed && !cancellationToken.IsCancellationRequested && operation.IsCancellationRequested)
                ReportFailure("TaskMutationTimedOut");
        }
        finally { _blockedTaskId = null; }
    }

    private void ReportFailure(string key)
    {
        HasStatusError = true;
        Status = SettingsText.Get(key);
    }

    private void HandleMutationChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(SettingsMutationRunner.IsRunning)) return;
        OnPropertyChanged(nameof(IsBusy));
        NotifyCommands();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _operations.Dispose();
        _mutations.PropertyChanged -= HandleMutationChanged;
        SaveRenameCommand.Cancel();
        ArchiveCommand.Cancel();
        TogglePinCommand.Cancel();
        RestoreCommand.Cancel();
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        BeginRenameCommand.NotifyCanExecuteChanged();
        ArchiveCommand.NotifyCanExecuteChanged();
        TogglePinCommand.NotifyCanExecuteChanged();
        SaveRenameCommand.NotifyCanExecuteChanged();
        RestoreCommand.NotifyCanExecuteChanged();
    }
}
