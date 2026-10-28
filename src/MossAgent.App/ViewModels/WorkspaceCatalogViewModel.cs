using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Authorization;
using MossAgent.Application.Git;
using MossAgent.Application.Persistence;
using MossAgent.App.Browser;
using MossAgent.App.Services;
using MossAgent.Domain;
namespace MossAgent.App.ViewModels;
public sealed class WorkspaceCatalogViewModel : ObservableObject
{
    private readonly IWorkspaceRepository _workspaces;
    private readonly ConversationAttachmentThumbnailFactory? _thumbnails;
    private readonly WorkspaceAuthorizationDrainTracker? _drainTracker;
    private ProjectProfile? _selectedProject;
    private AgentTask? _selectedTask;
    private string _status = string.Empty;
    private bool _isLocked;
    private readonly WorkspaceNavigationController _navigation;
    private readonly Dictionary<Guid, AgentTask> _projectlessTasks = [];
    private Guid? _pendingTaskId;
    private bool _pendingEmptySession;
    private int _loadVersion;
    private bool _projectlessSelected;
    public WorkspaceCatalogViewModel(
        IWorkspaceRepository workspaces,
        IGitWorktreeAuthorizationService? worktreeAuthorizations,
        WorkspaceProjectService projectService, ConversationAttachmentThumbnailFactory? thumbnails = null,
        IGitWorktreeReferenceDiscovery? worktreeReferences = null,
        ProjectAuthorizationMonitor? authorizations = null)
    {
        _workspaces = workspaces;
        Environment = new WorkspaceTaskEnvironmentViewModel(worktreeReferences);
        _thumbnails = thumbnails;
        if (authorizations is not null)
            _drainTracker = new WorkspaceAuthorizationDrainTracker(authorizations,
                projectId => ProjectNavigation.FindProjectGroup(projectId));
        WorktreeAuthorization = new(worktreeAuthorizations);
        ProjectEditor = new WorkspaceProjectEditorViewModel(projectService);
        ProjectEditor.ProjectCreated += AddProject;
        ProjectEditor.ProjectUpdated += UpdateProject;
        ProjectEditor.CanSave = () => !IsLocked;
        TaskSearch = new WorkspaceTaskSearchViewModel();
        RecentTasks = new WorkspaceRecentTasksViewModel();
        TaskSearch.PropertyChanged += HandleTaskSearchPropertyChanged;
        TaskManagement = new WorkspaceTaskManagementViewModel(workspaces)
        {
            IsTaskRunning = taskId => IsTaskRunning?.Invoke(taskId) == true || WorktreeAuthorization.BlocksTaskStart(taskId)
        };
        TaskManagement.TaskRenamed += task => UpsertTask(task, select: SelectedTask?.Id == task.Id);
        TaskManagement.TaskArchived += ArchiveTask;
        TaskManagement.TaskRestored += RestoreTask;
        TaskManagement.TaskPinChanged += task => UpsertTask(task, select: SelectedTask?.Id == task.Id);
        NewTaskCommand = new RelayCommand(StartNewTask, () => !IsLocked);
        NewChatCommand = new RelayCommand(StartNewChat, () => !IsLocked);
        SelectProjectCommand = new RelayCommand<ProjectProfile>(SelectProject);
        EditProjectCommand = new RelayCommand<WorkspaceProjectNavigationViewModel>(
            OpenProjectEditor, _ => !IsLocked);
        SelectTaskCommand = new RelayCommand<AgentTask>(SelectTask);
        VerifyDrainCommand = new AsyncRelayCommand<WorkspaceProjectNavigationViewModel>(VerifyDrainAsync);
        _navigation = new WorkspaceNavigationController(() => (SelectedProject?.Id, SelectedTask?.Id),
            () => !IsLocked, NavigateToHistoryTarget);
        NavigateBackCommand = _navigation.BackCommand;
        NavigateForwardCommand = _navigation.ForwardCommand;
    }
    public event Action? SelectionChanged;
    public event Action? TaskRunStateChanged;
    public event Action? ConversationRequested;
    public Func<Guid, IReadOnlyList<ChatMessageViewModel>?>? ActiveMessagesProvider { get; set; }
    public Func<Guid, bool>? IsTaskRunning { get; set; }
    public ObservableCollection<ProjectProfile> Projects { get; } = [];
    public ObservableCollection<WorkspaceProjectNavigationViewModel> ProjectNavigation { get; } = [];
    public ObservableCollection<AgentTask> Tasks => TaskSearch.Results;
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];
    public WorkspaceProjectEditorViewModel ProjectEditor { get; }
    public WorkspaceSidebarSectionViewModel Sections { get; } = new();
    public WorkspaceTaskSearchViewModel TaskSearch { get; }
    public WorkspaceRecentTasksViewModel RecentTasks { get; }
    public WorkspaceTaskManagementViewModel TaskManagement { get; }
    public WorkspaceTaskEnvironmentViewModel Environment { get; }
    public WorkspaceWorktreeAuthorizationViewModel WorktreeAuthorization { get; }
    public IRelayCommand NewTaskCommand { get; }
    public IRelayCommand NewChatCommand { get; }
    public string ProjectlessChatLabel => WorkspaceText.Get(nameof(ProjectlessChatLabel));
    public IRelayCommand<ProjectProfile> SelectProjectCommand { get; }
    public IRelayCommand<WorkspaceProjectNavigationViewModel> EditProjectCommand { get; }
    public IRelayCommand<AgentTask> SelectTaskCommand { get; }
    /// <summary>侧栏项目行的人工核实入口：仅对清理未知行触发租约级复核。</summary>
    public IAsyncRelayCommand<WorkspaceProjectNavigationViewModel> VerifyDrainCommand { get; }
    public IRelayCommand NavigateBackCommand { get; }
    public IRelayCommand NavigateForwardCommand { get; }
    public ProjectProfile? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (_isLocked || !SetProperty(ref _selectedProject, value))
            {
                return;
            }
            if (value is not null)
            {
                Sections.ExpandProjects();
            }
            _projectlessSelected = value is null;
            SetSelectedTask(null, loadMessages: false);
            foreach (var group in ProjectNavigation)
            {
                group.IsSelected = group.Project.Id == value?.Id;
                if (group.IsSelected)
                {
                    group.IsExpanded = true;
                }
            }
            NotifySelectionChanged();
            _ = LoadTasksAsync(value, Interlocked.Increment(ref _loadVersion));
        }
    }
    public AgentTask? SelectedTask
    {
        get => _selectedTask;
        set { if (!_isLocked) SetSelectedTask(value, loadMessages: true); }
    }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (SetProperty(ref _isLocked, value))
            {
                Environment.SetLocked(value);
                NewTaskCommand.NotifyCanExecuteChanged();
                NewChatCommand.NotifyCanExecuteChanged();
                EditProjectCommand.NotifyCanExecuteChanged();
                ProjectEditor.NotifyAvailabilityChanged();
                _navigation.NotifyAvailabilityChanged();
                TaskManagement.NotifyTaskRunStateChanged();
                RefreshWorktreeAuthorization();
            }
        }
    }
    public async Task ReloadAsync()
    {
        var selectedProjectId = SelectedProject?.Id;
        var selectedTaskId = SelectedTask?.Id;
        var projects = await _workspaces.GetProjectsAsync(CancellationToken.None);
        var navigation = await WorkspaceProjectNavigationLoader.LoadAsync(
            _workspaces, projects, ProjectNavigation, TaskSearch.Query);
        Projects.ReplaceWith(projects);
        ProjectNavigation.ReplaceWith(navigation);
        foreach (var group in navigation) _drainTracker?.Refresh(group);
        _projectlessTasks.Clear();
        foreach (var task in await _workspaces.GetTasksAsync(null, CancellationToken.None))
            _projectlessTasks[task.Id] = task;
        RecentTasks.SetTasks(
            navigation.SelectMany(group => group.TaskSnapshot).Concat(_projectlessTasks.Values).Where(task => !task.IsPinned),
            TaskSearch.Query);
        await TaskManagement.LoadPinnedAsync(TaskSearch.Query);
        _pendingTaskId = selectedTaskId;
        _pendingEmptySession = selectedTaskId is null && (selectedProjectId is not null || _projectlessSelected);
        _selectedProject = null;
        OnPropertyChanged(nameof(SelectedProject));
        var targetProject = _projectlessSelected ? null
            : Projects.FindProject(selectedProjectId) ?? Projects.FirstOrDefault();
        if (selectedTaskId is not null && _projectlessTasks.ContainsKey(selectedTaskId.Value)) targetProject = null;
        if (targetProject is null) await LoadTasksAsync(null, Interlocked.Increment(ref _loadVersion));
        else SelectedProject = targetProject;
    }
    public void UpsertTask(AgentTask task, bool select = true)
    {
        var isPinned = TaskManagement.PinnedTasks.IsPinned(task.Id);
        if (task.IsPinned != isPinned) task = task with { IsPinned = isPinned };
        ProjectNavigation.FindProjectGroup(task.ProjectId)?.UpsertTask(task);
        if (task.ProjectId is null) _projectlessTasks[task.Id] = task;
        RecentTasks.Upsert(task);
        TaskManagement.PinnedTasks.Upsert(task);
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
        if (_isLocked) return;
        ConversationRequested?.Invoke();
        _navigation.Record((SelectedProject?.Id, null));
        StartNewTaskCore();
    }

    private void StartNewChat()
    {
        if (_isLocked) return;
        ConversationRequested?.Invoke();
        _navigation.Record((null, null));
        _projectlessSelected = true;
        _pendingTaskId = null;
        _pendingEmptySession = true;
        if (SelectedProject is null) StartNewTaskCore();
        else SelectedProject = null;
    }

    public AgentTask? FindTask(Guid? projectId, Guid taskId) => projectId is null
        ? _projectlessTasks.GetValueOrDefault(taskId)
        : ProjectNavigation.FindProjectGroup(projectId)?.FindTask(taskId);

    private void StartNewTaskCore()
    {
        _pendingTaskId = null;
        _pendingEmptySession = true;
        SetSelectedTask(null, loadMessages: false);
        Messages.Clear();
    }

    /// <summary>通过侧栏标题切换项目，并清除之前的跨项目任务定位。</summary>
    private void SelectProject(ProjectProfile? project)
    {
        if (_isLocked || project is null || project.Id == SelectedProject?.Id)
        {
            return;
        }
        ConversationRequested?.Invoke();
        _navigation.Record((project.Id, null));
        _pendingTaskId = null;
        _pendingEmptySession = false;
        SelectedProject = project;
    }

    /// <summary>打开侧栏任务；不同项目的任务在项目数据加载完成后再选中。</summary>
    private void SelectTask(AgentTask? task)
    {
        if (_isLocked || task is null)
        {
            return;
        }
        var project = Projects.FindProject(task.ProjectId);
        if (task.ProjectId is not null && project is null)
        {
            return;
        }
        ConversationRequested?.Invoke();
        TaskSearch.Query = string.Empty;
        _navigation.Record((task.ProjectId, task.Id));
        _pendingTaskId = null;
        _pendingEmptySession = false;
        if (SelectedProject?.Id == task.ProjectId)
        {
            SelectedTask = task;
            return;
        }
        _pendingTaskId = task.Id;
        _pendingEmptySession = false;
        SelectedProject = project;
    }

    private void NavigateToHistoryTarget((Guid? ProjectId, Guid? TaskId) target)
    {
        TaskSearch.Query = string.Empty;
        var project = Projects.FindProject(target.ProjectId);
        if (target.ProjectId is not null && project is null)
        {
            return;
        }

        var task = target.TaskId is Guid taskId
            ? FindTask(target.ProjectId, taskId)
            : null;
        if (target.TaskId is not null && task is null)
        {
            _navigation.RemoveTask(target.TaskId.Value);
            return;
        }

        ConversationRequested?.Invoke();
        _pendingTaskId = target.TaskId;
        _pendingEmptySession = target.TaskId is null;
        if (project?.Id == SelectedProject?.Id)
        {
            if (task is null)
            {
                StartNewTaskCore();
            }
            else
            {
                _pendingTaskId = null;
                _pendingEmptySession = false;
                SetSelectedTask(task, loadMessages: true);
            }
            return;
        }

        SelectedProject = project;
    }

    /// <summary>
    /// 将搜索词同步到所有项目分组，使项目名和任务标题都可被检索。
    /// </summary>
    private void HandleTaskSearchPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(WorkspaceTaskSearchViewModel.Query))
        {
            return;
        }
        if (!string.IsNullOrWhiteSpace(TaskSearch.Query))
        {
            Sections.ExpandProjects();
        }
        foreach (var group in ProjectNavigation)
        {
            group.ApplyFilter(TaskSearch.Query);
        }
        RecentTasks.ApplyFilter(TaskSearch.Query);
        TaskManagement.PinnedTasks.ApplyFilter(TaskSearch.Query);
    }

    private void AddProject(ProjectProfile project)
    {
        Projects.Add(project);
        var group = new WorkspaceProjectNavigationViewModel(project);
        group.ApplyFilter(TaskSearch.Query);
        _drainTracker?.Refresh(group);
        ProjectNavigation.Add(group);
        SelectedProject = project;
    }

    private void OpenProjectEditor(WorkspaceProjectNavigationViewModel? group)
    {
        if (group is null) return;
        group.IsSummaryOpen = false;
        ProjectEditor.Edit(group.Project);
    }

    /// <summary>对清理未知项目行触发人工核实；进行中与未知以外的状态直接忽略。</summary>
    private async Task VerifyDrainAsync(WorkspaceProjectNavigationViewModel? row,
        CancellationToken cancellationToken)
    {
        if (_drainTracker is null
            || row?.AuthorizationDrain != AuthorizationDrainState.CleanupUnknown
            || row.IsVerifyingDrain) return;
        await AvaloniaUiThread.RunAsync(() => row.IsVerifyingDrain = true, cancellationToken);
        try
        {
            await _drainTracker.VerifyCleanupAsync(row.Project.Id, cancellationToken);
        }
        finally
        {
            await AvaloniaUiThread.RunAsync(() => row.IsVerifyingDrain = false, CancellationToken.None);
        }
    }

    private void UpdateProject(ProjectProfile project)
    {
        var index = Projects.FindProjectIndex(project.Id);
        if (index < 0) return;
        Projects[index] = project;
        var group = ProjectNavigation.FindProjectGroup(project.Id);
        group?.UpdateProject(project);
        if (group is not null) _drainTracker?.Refresh(group);
        if (SelectedProject?.Id == project.Id &&
            SetProperty(ref _selectedProject, project, nameof(SelectedProject))) NotifySelectionChanged();
    }
    private async Task LoadTasksAsync(ProjectProfile? project, int version)
    {
        TaskSearch.SetItems([]);
        SetSelectedTask(null, loadMessages: false);
        Messages.Clear();
        var tasks = await _workspaces.GetTasksAsync(project?.Id, CancellationToken.None);
        if (version != _loadVersion || project?.Id != SelectedProject?.Id) return;
        await TaskManagement.LoadAsync(project?.Id);
        if (version != _loadVersion || project?.Id != SelectedProject?.Id)
        {
            return;
        }
        ProjectNavigation.FindProjectGroup(project?.Id)?.SetTasks(tasks);
        if (project is null)
        {
            _projectlessTasks.Clear();
            foreach (var task in tasks) _projectlessTasks[task.Id] = task;
        }
        TaskSearch.SetItems(tasks);
        var requestedTaskId = _pendingTaskId;
        var restoreEmptySession = _pendingEmptySession;
        _pendingTaskId = null;
        _pendingEmptySession = false;
        if (SelectedTask is { } current && current.ProjectId == project?.Id)
        {
            UpsertTask(current);
            return;
        }
        var selectedTask = restoreEmptySession
            ? null
            : tasks.FindTask(requestedTaskId) ?? Tasks.FirstOrDefault();
        SetSelectedTask(selectedTask, loadMessages: selectedTask is not null);
    }
    internal async Task LoadMessagesAsync(AgentTask task)
    {
        if (TryShowActiveMessages(task.Id)) return;
        var messages = await _workspaces.GetMessagesAsync(task.Id, CancellationToken.None);
        if (task.Id != SelectedTask?.Id) return;
        if (TryShowActiveMessages(task.Id)) return;
        Messages.ReplaceWith(messages.Select(message => WorkspaceConversationMapper.ToChatMessage(message, _thumbnails)));
    }
    private void SetSelectedTask(AgentTask? task, bool loadMessages)
    {
        if (!SetProperty(ref _selectedTask, task, nameof(SelectedTask)))
        {
            return;
        }
        if (task is not null) _projectlessSelected = task.ProjectId is null;
        foreach (var group in ProjectNavigation)
        {
            group.SetSelectedTask(task?.Id);
        }
        RecentTasks.SetSelectedTask(task?.Id);
        TaskManagement.SelectedTask = task;
        NotifySelectionChanged();
        if (loadMessages && task is not null)
        {
            _ = LoadMessagesAsync(task);
        }
    }
    private void NotifySelectionChanged()
    {
        Environment.SetContext(SelectedProject, SelectedTask);
        RefreshWorktreeAuthorization();
        SelectionChanged?.Invoke();
    }
    public void NotifyTaskRunStateChanged()
    {
        RefreshWorktreeAuthorization();
        TaskManagement.NotifyTaskRunStateChanged();
        TaskRunStateChanged?.Invoke();
    }
    private void RefreshWorktreeAuthorization() => WorktreeAuthorization.SetContext(SelectedProject, SelectedTask,
        !IsLocked && (SelectedTask is null || IsTaskRunning?.Invoke(SelectedTask.Id) != true));
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
        var unchanged = Messages.Count == active.Count;
        for (var index = 0; unchanged && index < active.Count; index++)
            unchanged = ReferenceEquals(Messages[index], active[index]);
        if (!unchanged) Messages.SynchronizeWith(active);
        return true;
    }
    private void ArchiveTask(AgentTask task)
    {
        _navigation.RemoveTask(task.Id);
        ProjectNavigation.FindProjectGroup(task.ProjectId)?.RemoveTask(task.Id);
        _projectlessTasks.Remove(task.Id);
        RecentTasks.Remove(task.Id);
        TaskSearch.Remove(task.Id);
        if (SelectedTask?.Id == task.Id)
        {
            SetSelectedTask(null, loadMessages: false);
            Messages.Clear();
        }
    }
    private void RestoreTask(AgentTask task)
    {
        UpsertTask(task, select: false);
        SelectTask(task);
    }
}

