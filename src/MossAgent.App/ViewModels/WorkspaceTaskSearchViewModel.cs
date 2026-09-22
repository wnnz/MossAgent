using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceTaskSearchViewModel : ObservableObject
{
    private readonly List<AgentTask> _allTasks = [];
    private string _query = string.Empty;

    public WorkspaceTaskSearchViewModel()
    {
        ClearCommand = new RelayCommand(() => Query = string.Empty, () => HasQuery);
    }

    public ObservableCollection<AgentTask> Results { get; } = [];

    public IRelayCommand ClearCommand { get; }

    public string Query
    {
        get => _query;
        set
        {
            if (SetProperty(ref _query, value))
            {
                OnPropertyChanged(nameof(HasQuery));
                ClearCommand.NotifyCanExecuteChanged();
                ApplyFilter();
            }
        }
    }

    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);

    public void SetItems(IEnumerable<AgentTask> tasks)
    {
        _allTasks.Clear();
        _allTasks.AddRange(tasks);
        ApplyFilter();
    }

    public void Upsert(AgentTask task)
    {
        var index = _allTasks.FindIndex(item => item.Id == task.Id);
        if (index >= 0)
        {
            _allTasks[index] = task;
        }
        else
        {
            _allTasks.Insert(0, task);
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = Query.Trim();
        var filtered = query.Length == 0
            ? _allTasks
            : _allTasks.Where(task =>
                task.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        Results.ReplaceWith(filtered);
    }
}
