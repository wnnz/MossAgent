using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceTaskManagementViewModel : ObservableObject
{
    private readonly IWorkspaceRepository _workspaces;
    private AgentTask? _selectedTask;
    private AgentTask? _selectedArchivedTask;
    private string _editingTitle = string.Empty;
    private string _status = string.Empty;
    private bool _isEditing;
    private bool _isArchiveVisible;
    private int _loadVersion;

    public WorkspaceTaskManagementViewModel(IWorkspaceRepository workspaces)
    {
        _workspaces = workspaces;
        BeginRenameCommand = new RelayCommand(BeginRename, CanModifySelectedTask);
        SaveRenameCommand = new AsyncRelayCommand(SaveRenameAsync, CanSaveRename);
        CancelRenameCommand = new RelayCommand(CancelRename);
        ArchiveCommand = new AsyncRelayCommand(ArchiveAsync, CanModifySelectedTask);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync, () => SelectedArchivedTask is not null);
        ToggleArchiveCommand = new RelayCommand(ToggleArchive);
    }

    public event Action<AgentTask>? TaskRenamed;
    public event Action<AgentTask>? TaskArchived;
    public event Action<AgentTask>? TaskRestored;
    public Func<Guid, bool>? IsTaskRunning { get; set; }
    public ObservableCollection<AgentTask> ArchivedTasks { get; } = [];
    public IRelayCommand BeginRenameCommand { get; }
    public IAsyncRelayCommand SaveRenameCommand { get; }
    public IRelayCommand CancelRenameCommand { get; }
    public IAsyncRelayCommand ArchiveCommand { get; }
    public IAsyncRelayCommand RestoreCommand { get; }
    public IRelayCommand ToggleArchiveCommand { get; }

    public AgentTask? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (!SetProperty(ref _selectedTask, value)) return;
            CancelRename();
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
                SaveRenameCommand.NotifyCanExecuteChanged();
        }
    }

    public string Status { get => _status; private set => SetProperty(ref _status, value); }
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
    public string ArchiveToggleLabel => IsArchiveVisible
        ? $"隐藏已归档 ({ArchivedTasks.Count})"
        : $"已归档 ({ArchivedTasks.Count})";
    public bool HasArchivedTasks => ArchivedTasks.Count > 0;

    public async Task LoadAsync(Guid? projectId)
    {
        var version = Interlocked.Increment(ref _loadVersion);
        ArchivedTasks.Clear();
        SelectedArchivedTask = null;
        OnPropertyChanged(nameof(ArchiveToggleLabel));
        OnPropertyChanged(nameof(HasArchivedTasks));
        if (projectId is null) return;
        var tasks = await _workspaces.GetArchivedTasksAsync(projectId.Value, CancellationToken.None);
        if (version != _loadVersion) return;
        ArchivedTasks.ReplaceWith(tasks);
        OnPropertyChanged(nameof(ArchiveToggleLabel));
        OnPropertyChanged(nameof(HasArchivedTasks));
    }

    public void NotifyTaskRunStateChanged() => NotifyCommands();

    private void BeginRename()
    {
        EditingTitle = SelectedTask?.Title ?? string.Empty;
        IsEditing = SelectedTask is not null;
        SaveRenameCommand.NotifyCanExecuteChanged();
    }

    private async Task SaveRenameAsync()
    {
        if (SelectedTask is not { } task) return;
        var updated = task with { Title = EditingTitle.Trim(), UpdatedAt = DateTimeOffset.UtcNow };
        await _workspaces.SaveTaskAsync(updated, CancellationToken.None);
        SelectedTask = updated;
        TaskRenamed?.Invoke(updated);
        Status = "任务已重命名。";
    }

    private void CancelRename()
    {
        IsEditing = false;
        EditingTitle = string.Empty;
    }

    private async Task ArchiveAsync()
    {
        if (SelectedTask is not { } task) return;
        await _workspaces.SetTaskArchivedAsync(task.Id, true, CancellationToken.None);
        ArchivedTasks.Insert(0, task);
        OnPropertyChanged(nameof(ArchiveToggleLabel));
        OnPropertyChanged(nameof(HasArchivedTasks));
        TaskArchived?.Invoke(task);
        Status = "任务已归档。";
    }

    private async Task RestoreAsync()
    {
        if (SelectedArchivedTask is not { } task) return;
        await _workspaces.SetTaskArchivedAsync(task.Id, false, CancellationToken.None);
        ArchivedTasks.Remove(task);
        SelectedArchivedTask = null;
        OnPropertyChanged(nameof(ArchiveToggleLabel));
        OnPropertyChanged(nameof(HasArchivedTasks));
        TaskRestored?.Invoke(task);
        Status = "任务已恢复。";
    }

    private void ToggleArchive() => IsArchiveVisible = !IsArchiveVisible;
    private bool CanModifySelectedTask() =>
        SelectedTask is { } task && IsTaskRunning?.Invoke(task.Id) != true;
    private bool CanSaveRename() =>
        IsEditing && CanModifySelectedTask() && !string.IsNullOrWhiteSpace(EditingTitle);

    private void NotifyCommands()
    {
        BeginRenameCommand.NotifyCanExecuteChanged();
        ArchiveCommand.NotifyCanExecuteChanged();
        SaveRenameCommand.NotifyCanExecuteChanged();
    }
}
