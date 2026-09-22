using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
namespace MossAgent.App.ViewModels;

public sealed class WorkspaceCatalogViewModel : ObservableObject
{
    private readonly IWorkspaceRepository _workspaces;
    private readonly WorkspaceWorktreeCleanupService _worktreeCleanup;
    private ProjectProfile? _selectedProject;
    private AgentTask? _selectedTask;
    private string _status = string.Empty;
    private bool _isLocked;
    private Guid? _cleanupConfirmationTaskId;
    private int _loadVersion;
    public WorkspaceCatalogViewModel(
        IWorkspaceRepository workspaces,
        WorkspaceWorktreeCleanupService worktreeCleanup,
        WorkspaceProjectService projectService)
    {
        _workspaces = workspaces;
        _worktreeCleanup = worktreeCleanup;
        ProjectEditor = new WorkspaceProjectEditorViewModel(projectService);
        ProjectEditor.ProjectCreated += AddProject;
        TaskSearch = new WorkspaceTaskSearchViewModel();
        TaskManagement = new WorkspaceTaskManagementViewModel(workspaces)
        {
            IsTaskRunning = taskId => IsTaskRunning?.Invoke(taskId) == true
        };
        TaskManagement.TaskRenamed += task => UpsertTask(task);
        TaskManagement.TaskArchived += ArchiveTask;
        TaskManagement.TaskRestored += RestoreTask;
        NewTaskCommand = new RelayCommand(StartNewTask, () => !IsLocked);
        CleanupWorktreeCommand = new AsyncRelayCommand(CleanupWorktreeAsync, CanCleanupWorktree);
    }
    public event Action? SelectionChanged;
    public Func<Guid, IReadOnlyList<ChatMessageViewModel>?>? ActiveMessagesProvider { get; set; }
    public Func<Guid, bool>? IsTaskRunning { get; set; }
    public ObservableCollection<ProjectProfile> Projects { get; } = [];
    public ObservableCollection<AgentTask> Tasks => TaskSearch.Results;
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];
    public WorkspaceProjectEditorViewModel ProjectEditor { get; }
    public WorkspaceTaskSearchViewModel TaskSearch { get; }
    public WorkspaceTaskManagementViewModel TaskManagement { get; }
    public IRelayCommand NewTaskCommand { get; }
    public IAsyncRelayCommand CleanupWorktreeCommand { get; }
    public ProjectProfile? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (_isLocked || !SetProperty(ref _selectedProject, value))
            {
                return;
            }
            SelectionChanged?.Invoke();
            _ = LoadTasksAsync(value, Interlocked.Increment(ref _loadVersion));
        }
    }
    public AgentTask? SelectedTask
    {
        get => _selectedTask;
        set { if (!_isLocked) SetSelectedTask(value, loadMessages: true); }
    }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string CleanupWorktreeText =>
        _cleanupConfirmationTaskId == SelectedTask?.Id ? "确认清理 Worktree" : "清理 Worktree";
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (SetProperty(ref _isLocked, value))
            {
                NewTaskCommand.NotifyCanExecuteChanged();
                CleanupWorktreeCommand.NotifyCanExecuteChanged();
                TaskManagement.NotifyTaskRunStateChanged();
            }
        }
    }
    public async Task ReloadAsync()
    {
        var selectedProjectId = SelectedProject?.Id;
        var projects = await _workspaces.GetProjectsAsync(CancellationToken.None);
        Projects.ReplaceWith(projects);
        _selectedProject = null;
        OnPropertyChanged(nameof(SelectedProject));
        SelectedProject = Projects.FirstOrDefault(project => project.Id == selectedProjectId)
            ?? Projects.FirstOrDefault();
    }
    public void UpsertTask(AgentTask task, bool select = true)
    {
        if (SelectedProject?.Id != task.ProjectId)
        {
            return;
        }
        TaskSearch.Upsert(task);
        if (select)
        {
            SetSelectedTask(task, loadMessages: false);
        }
    }
    public void StartNewTask()
    {
        if (_isLocked)
        {
            return;
        }
        SetSelectedTask(null, loadMessages: false);
        Messages.Clear();
    }
    private void AddProject(ProjectProfile project)
    {
        Projects.Add(project);
        SelectedProject = project;
    }
    private async Task LoadTasksAsync(ProjectProfile? project, int version)
    {
        TaskSearch.SetItems([]);
        SetSelectedTask(null, loadMessages: false);
        Messages.Clear();
        if (project is null)
        {
            await TaskManagement.LoadAsync(null);
            return;
        }
        var tasks = await _workspaces.GetTasksAsync(project.Id, CancellationToken.None);
        await TaskManagement.LoadAsync(project.Id);
        if (version != _loadVersion || project.Id != SelectedProject?.Id)
        {
            return;
        }
        TaskSearch.SetItems(tasks);
        SetSelectedTask(Tasks.FirstOrDefault(), loadMessages: true);
    }
    private async Task LoadMessagesAsync(AgentTask task)
    {
        if (TryShowActiveMessages(task.Id))
        {
            return;
        }
        var messages = await _workspaces.GetMessagesAsync(task.Id, CancellationToken.None);
        if (task.Id != SelectedTask?.Id)
        {
            return;
        }
        if (TryShowActiveMessages(task.Id))
        {
            return;
        }
        Messages.ReplaceWith(messages.Select(WorkspaceConversationMapper.ToChatMessage));
    }
    private void SetSelectedTask(AgentTask? task, bool loadMessages)
    {
        if (!SetProperty(ref _selectedTask, task, nameof(SelectedTask)))
        {
            return;
        }
        CancelCleanupConfirmation();
        TaskManagement.SelectedTask = task;
        CleanupWorktreeCommand.NotifyCanExecuteChanged();
        SelectionChanged?.Invoke();
        if (loadMessages && task is not null)
        {
            _ = LoadMessagesAsync(task);
        }
    }
    private bool CanCleanupWorktree() =>
        !_isLocked
        && !string.IsNullOrWhiteSpace(SelectedTask?.WorktreePath)
        && (SelectedTask is null || IsTaskRunning?.Invoke(SelectedTask.Id) != true);
    public void NotifyTaskRunStateChanged()
    {
        CleanupWorktreeCommand.NotifyCanExecuteChanged();
        TaskManagement.NotifyTaskRunStateChanged();
    }
    public void ShowActiveMessages(Guid taskId)
    {
        if (SelectedTask?.Id == taskId)
        {
            TryShowActiveMessages(taskId);
        }
    }
    private bool TryShowActiveMessages(Guid taskId)
    {
        var active = ActiveMessagesProvider?.Invoke(taskId);
        if (active is null)
        {
            return false;
        }
        Messages.ReplaceWith(active);
        return true;
    }
    private async Task CleanupWorktreeAsync()
    {
        if (SelectedProject is not { } project || SelectedTask is not { WorktreePath: not null } task)
        {
            return;
        }
        if (_cleanupConfirmationTaskId != task.Id)
        {
            _cleanupConfirmationTaskId = task.Id;
            OnPropertyChanged(nameof(CleanupWorktreeText));
            Status = "再次点击确认清理；有未提交变更时 Git 会拒绝删除。";
            return;
        }
        try
        {
            var updated = await _worktreeCleanup.CleanupAsync(project, task, CancellationToken.None);
            UpsertTask(updated);
            Status = "任务 worktree 已安全移除，任务历史仍保留。";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
        finally
        {
            CancelCleanupConfirmation();
            CleanupWorktreeCommand.NotifyCanExecuteChanged();
        }
    }
    private void CancelCleanupConfirmation()
    {
        if (_cleanupConfirmationTaskId is null)
        {
            return;
        }
        _cleanupConfirmationTaskId = null;
        OnPropertyChanged(nameof(CleanupWorktreeText));
    }
    private void ArchiveTask(AgentTask task)
    {
        TaskSearch.Remove(task.Id);
        if (SelectedTask?.Id == task.Id) SetSelectedTask(null, loadMessages: false);
    }
    private void RestoreTask(AgentTask task)
    {
        TaskSearch.Restore(task);
        SetSelectedTask(task, loadMessages: true);
    }
}
