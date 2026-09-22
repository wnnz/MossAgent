using MossAgent.Application.Git;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceWorktreeCleanupService(
    IWorkspaceRepository workspaces,
    IGitWorktreeService worktrees)
{
    public async Task<AgentTask> CleanupAsync(
        ProjectProfile project,
        AgentTask task,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(task.WorktreePath))
        {
            throw new InvalidOperationException("当前任务没有托管 worktree。");
        }

        await worktrees.RemoveAsync(project, task.WorktreePath, cancellationToken);
        var updated = task with
        {
            WorktreePath = null,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await workspaces.SaveTaskAsync(updated, cancellationToken);
        return updated;
    }
}
