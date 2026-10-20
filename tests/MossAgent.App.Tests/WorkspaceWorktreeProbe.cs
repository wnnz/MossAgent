using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class WorkspaceWorktreeProbe : IGitWorktreeService
{
    public List<(ProjectProfile Project, Guid TaskId)> Created { get; } = [];
    public List<string> Removed { get; } = [];
    public bool RemovalWasCancelled { get; private set; }
    public Func<CancellationToken, Task>? CreateHandler { get; set; }
    public Func<CancellationToken, Task>? RemoveHandler { get; set; }

    public Task ValidateAsync(ProjectProfile project, Guid taskId, string worktreePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Created.Any(item => item.Project.Id == project.Id && item.TaskId == taskId)
            || worktreePath != Path.Combine(Path.GetTempPath(), "MossAgent.Tests", "worktrees", taskId.ToString("N")))
            throw new UnauthorizedAccessException("The probe does not own this worktree.");
        return Task.CompletedTask;
    }

    public async Task<string> CreateAsync(ProjectProfile project, Guid taskId, CancellationToken cancellationToken, string? baseReference = null)
    {
        Created.Add((project, taskId));
        if (CreateHandler is not null) await CreateHandler(cancellationToken);
        return Path.Combine(Path.GetTempPath(), "MossAgent.Tests", "worktrees", taskId.ToString("N"));
    }

    public async Task RemoveAsync(ProjectProfile project, string worktreePath, CancellationToken cancellationToken)
    {
        Removed.Add(worktreePath);
        RemovalWasCancelled = cancellationToken.IsCancellationRequested;
        if (RemoveHandler is not null) await RemoveHandler(cancellationToken);
    }
}
