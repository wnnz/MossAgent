using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal static class WorkspaceProjectNavigationLoader
{
    public static async Task<IReadOnlyList<WorkspaceProjectNavigationViewModel>> LoadAsync(
        IWorkspaceRepository repository, IReadOnlyList<ProjectProfile> projects,
        IEnumerable<WorkspaceProjectNavigationViewModel> previous, string query)
    {
        var expanded = previous.ToDictionary(group => group.Project.Id, group => group.IsExpanded);
        var groups = new List<WorkspaceProjectNavigationViewModel>();
        foreach (var project in projects)
        {
            var group = new WorkspaceProjectNavigationViewModel(project)
            {
                IsExpanded = expanded.GetValueOrDefault(project.Id)
            };
            group.SetTasks(await repository.GetTasksAsync(project.Id, CancellationToken.None));
            group.ApplyFilter(query);
            groups.Add(group);
        }
        return groups;
    }
}
