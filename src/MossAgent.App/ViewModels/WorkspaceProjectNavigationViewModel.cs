using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

/// <summary>
/// 侧栏中的项目及其未归档任务，供项目和会话连续展示。
/// </summary>
public sealed class WorkspaceProjectNavigationViewModel : ObservableObject
{
    /// <summary>保留完整任务列表，以便搜索清除后恢复项目会话。</summary>
    private readonly List<AgentTask> _allTasks = [];
    private ProjectProfile _project;
    /// <summary>当前搜索词；空字符串表示展示全部项目与会话。</summary>
    private string _query = string.Empty;
    /// <summary>项目任务是否在侧栏中展开。</summary>
    private bool _isExpanded;
    private bool _isSummaryOpen;
    /// <summary>项目是否为当前工作区。</summary>
    private bool _isSelected;
    /// <summary>当前会话任务标识，用于搜索筛选后恢复高亮。</summary>
    private Guid? _selectedTaskId;
    /// <summary>当前搜索下项目标题或会话是否匹配。</summary>
    private bool _isVisible = true;

    /// <summary>
    /// 项目资料，点击标题时用于切换当前工作区。
    /// </summary>
    public WorkspaceProjectNavigationViewModel(ProjectProfile project)
    {
        _project = project;
        ToggleExpandedCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    public ProjectProfile Project
    {
        get => _project;
        private set => SetProperty(ref _project, value);
    }

    public string ProjectTaskSummary => UiText.T("{0} 个任务", _allTasks.Count);
    public DateTimeOffset LastActivityAt => _allTasks.Count == 0
        ? Project.CreatedAt : _allTasks.Max(task => task.UpdatedAt);

    public void UpdateProject(ProjectProfile project)
    {
        var activityChanged = _allTasks.Count == 0 && Project.CreatedAt != project.CreatedAt;
        Project = project;
        ApplyFilter(_query);
        if (activityChanged) OnPropertyChanged(nameof(LastActivityAt));
    }

    public IRelayCommand ToggleExpandedCommand { get; }

    /// <summary>
    /// 此项目下按最近更新时间排序的未归档任务。
    /// </summary>
    public ObservableCollection<WorkspaceTaskNavigationViewModel> Tasks { get; } = [];
    public WorkspaceProjectTaskPreviewViewModel TaskPreview { get; } = new();
    internal IReadOnlyList<AgentTask> TaskSnapshot => _allTasks;
    public AgentTask? FindTask(Guid taskId) => _allTasks.FindTask(taskId);

    /// <summary>
    /// 当前搜索条件下是否显示此项目分组。
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>
    /// 是否展开该项目下的任务列表。
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsSummaryOpen
    {
        get => _isSummaryOpen;
        set => SetProperty(ref _isSummaryOpen, value);
    }

    /// <summary>
    /// 此项目是否为当前工作区，用于标示侧栏选中状态。
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// 用持久化仓库中的最新任务替换此项目的侧栏条目。
    /// </summary>
    public void SetTasks(IEnumerable<AgentTask> tasks)
    {
        _allTasks.Clear();
        _allTasks.AddRange(tasks);
        RefreshTaskSnapshot();
    }

    /// <summary>
    /// 按项目名称和任务标题过滤此项目分组。
    /// </summary>
    public void ApplyFilter(string query)
    {
        _query = query.Trim();
        var projectMatches = Project.Name.Contains(
            _query, StringComparison.CurrentCultureIgnoreCase);
        Tasks.ReplaceTasks(_allTasks, _selectedTaskId, projectMatches ? string.Empty : _query, excludePinned: true);
        TaskPreview.Update(Tasks, _query.Length != 0);
        IsVisible = _query.Length == 0 || projectMatches || Tasks.Count > 0;
    }

    /// <summary>
    /// 更新此项目下当前会话的选中状态。
    /// </summary>
    public void SetSelectedTask(Guid? taskId)
    {
        var changed = _selectedTaskId != taskId;
        _selectedTaskId = taskId;
        Tasks.SelectTask(taskId);
        if (changed) TaskPreview.Reveal(taskId);
    }

    /// <summary>
    /// 将新建或更新的任务置于最近会话顶部，并避免重复条目。
    /// </summary>
    public void UpsertTask(AgentTask task)
    {
        _allTasks.RemoveAll(item => item.Id == task.Id);
        _allTasks.Insert(0, task);
        RefreshTaskSnapshot();
    }

    /// <summary>
    /// 从当前项目侧栏移除已归档的任务。
    /// </summary>
    public void RemoveTask(Guid taskId)
    {
        _allTasks.RemoveAll(item => item.Id == taskId);
        RefreshTaskSnapshot();
    }

    private void RefreshTaskSnapshot()
    {
        ApplyFilter(_query);
        OnPropertyChanged(nameof(ProjectTaskSummary));
        OnPropertyChanged(nameof(LastActivityAt));
    }
}
