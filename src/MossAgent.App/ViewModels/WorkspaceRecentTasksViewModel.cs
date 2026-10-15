using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceRecentTasksViewModel : ObservableObject
{
    private readonly List<AgentTask> _allTasks = [];
    private string _query = string.Empty;
    private Guid? _selectedTaskId;
    private bool _hasItems;

    public ObservableCollection<WorkspaceTaskNavigationViewModel> Items { get; } = [];
    public bool HasItems { get => _hasItems; private set => SetProperty(ref _hasItems, value); }

    public void SetTasks(IEnumerable<AgentTask> tasks, string query)
    {
        _allTasks.Clear();
        _allTasks.AddRange(tasks
            .GroupBy(task => task.Id)
            .Select(group => group.MaxBy(task => task.UpdatedAt)!)
            .OrderByDescending(task => task.UpdatedAt));
        ApplyFilter(query);
    }

    public void ApplyFilter(string query)
    {
        _query = query.Trim();
        Items.ReplaceTasks(_allTasks, _selectedTaskId, _query, excludePinned: true);
        HasItems = Items.Count > 0;
    }

    public void Upsert(AgentTask task)
    {
        _allTasks.RemoveAll(item => item.Id == task.Id);
        var index = 0;
        while (index < _allTasks.Count && _allTasks[index].UpdatedAt >= task.UpdatedAt) index++;
        _allTasks.Insert(index, task);
        ApplyFilter(_query);
    }

    public void Remove(Guid taskId)
    {
        _allTasks.RemoveAll(item => item.Id == taskId);
        if (_selectedTaskId == taskId)
        {
            _selectedTaskId = null;
        }
        ApplyFilter(_query);
    }

    public void SetSelectedTask(Guid? taskId)
    {
        _selectedTaskId = taskId;
        Items.SelectTask(taskId);
    }
}
