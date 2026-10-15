using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal static class WorkspaceCatalogLookup
{
    public static ProjectProfile? FindProject(this IReadOnlyList<ProjectProfile> projects, Guid? projectId)
    {
        var index = projects.FindProjectIndex(projectId);
        return index < 0 ? null : projects[index];
    }

    public static int FindProjectIndex(this IReadOnlyList<ProjectProfile> projects, Guid? projectId)
    {
        if (projectId is null) return -1;
        for (var index = 0; index < projects.Count; index++)
        {
            if (projects[index].Id == projectId) return index;
        }
        return -1;
    }

    public static WorkspaceProjectNavigationViewModel? FindProjectGroup(
        this IReadOnlyList<WorkspaceProjectNavigationViewModel> groups, Guid? projectId)
    {
        if (projectId is null) return null;
        for (var index = 0; index < groups.Count; index++)
        {
            if (groups[index].Project.Id == projectId) return groups[index];
        }
        return null;
    }

    public static AgentTask? FindTask(this IReadOnlyList<AgentTask> tasks, Guid? taskId)
    {
        if (taskId is null) return null;
        for (var index = 0; index < tasks.Count; index++)
        {
            if (tasks[index].Id == taskId) return tasks[index];
        }
        return null;
    }
}
