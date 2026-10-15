using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspacePinnedTasksViewModel : ObservableObject
{
    private readonly List<AgentTask> _allTasks = [];
    private string _query = string.Empty;
    private Guid? _selectedTaskId;
    private bool _hasItems;

    public ObservableCollection<WorkspaceTaskNavigationViewModel> Items { get; } = [];
    public bool HasItems { get => _hasItems; private set => SetProperty(ref _hasItems, value); }
    public bool IsPinned(Guid taskId) => _allTasks.Any(task => task.Id == taskId);

    public void SetTasks(IEnumerable<AgentTask> tasks, string query)
    {
        _allTasks.Clear();
        _allTasks.AddRange(tasks.Where(task => task.IsPinned).DistinctBy(task => task.Id));
        ApplyFilter(query);
    }

    public void ApplyFilter(string query)
    {
        _query = query.Trim();
        Items.ReplaceTasks(_allTasks, _selectedTaskId, _query);
        HasItems = Items.Count > 0;
    }

    public void Upsert(AgentTask task)
    {
        _allTasks.RemoveAll(item => item.Id == task.Id);
        if (task.IsPinned) _allTasks.Insert(0, task);
        ApplyFilter(_query);
    }

    public void Remove(Guid taskId)
    {
        _allTasks.RemoveAll(item => item.Id == taskId);
        ApplyFilter(_query);
    }

    public void SetSelectedTask(Guid? taskId)
    {
        _selectedTaskId = taskId;
        Items.SelectTask(taskId);
    }
}
