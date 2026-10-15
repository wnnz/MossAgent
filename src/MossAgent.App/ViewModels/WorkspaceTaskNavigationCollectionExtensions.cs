using System.Collections.ObjectModel;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal static class WorkspaceTaskNavigationCollectionExtensions
{
    public static void ReplaceTasks(this ObservableCollection<WorkspaceTaskNavigationViewModel> target,
        IReadOnlyList<AgentTask> tasks, Guid? selectedTaskId, string query = "", bool excludePinned = false)
    {
        List<WorkspaceTaskNavigationViewModel>? rows = null;
        Dictionary<AgentTask, Queue<WorkspaceTaskNavigationViewModel>>? available = null;
        var index = 0;
        for (var sourceIndex = 0; sourceIndex < tasks.Count; sourceIndex++)
        {
            var task = tasks[sourceIndex];
            if ((excludePinned && task.IsPinned) ||
                (query.Length != 0 && !task.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase))) continue;
            WorkspaceTaskNavigationViewModel row;
            if (rows is null && index < target.Count && target[index].Task == task)
                row = target[index];
            else
            {
                if (rows is null)
                {
                    rows = new List<WorkspaceTaskNavigationViewModel>(index + 1);
                    for (var prefix = 0; prefix < index; prefix++) rows.Add(target[prefix]);
                    if (index < target.Count) available = CollectCandidates(target, index);
                }
                row = available is not null && available.TryGetValue(task, out var candidates)
                    && candidates.TryDequeue(out var retained) ? retained : new WorkspaceTaskNavigationViewModel(task);
                rows.Add(row);
            }
            row.IsSelected = row.Task.Id == selectedTaskId;
            index++;
        }
        if (rows is not null) target.SynchronizeWith(rows);
        else while (target.Count > index) target.RemoveAt(target.Count - 1);
    }

    private static Dictionary<AgentTask, Queue<WorkspaceTaskNavigationViewModel>> CollectCandidates(
        ObservableCollection<WorkspaceTaskNavigationViewModel> target, int start)
    {
        var available = new Dictionary<AgentTask, Queue<WorkspaceTaskNavigationViewModel>>(target.Count - start);
        for (var index = start; index < target.Count; index++)
        {
            var row = target[index];
            if (!available.TryGetValue(row.Task, out var candidates))
            {
                candidates = new Queue<WorkspaceTaskNavigationViewModel>();
                available.Add(row.Task, candidates);
            }
            candidates.Enqueue(row);
        }
        return available;
    }

    public static void SelectTask(this IEnumerable<WorkspaceTaskNavigationViewModel> rows, Guid? taskId)
    {
        foreach (var row in rows) row.IsSelected = row.Task.Id == taskId;
    }
}
