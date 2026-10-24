using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class RecordingGitWorktreeService : IGitWorktreeService
{
    public Exception? RemoveFailure { get; set; }
    public List<(Guid ProjectId, string Target)> RemovedTargets { get; } = [];

    public Task ValidateAsync(ProjectProfile project, Guid taskId, string worktreePath,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<string> CreateAsync(ProjectProfile project, Guid taskId,
        CancellationToken cancellationToken, string? baseReference = null) =>
        Task.FromResult(Path.Combine("worktrees", taskId.ToString("N")));

    public Task RemoveAsync(ProjectProfile project, string worktreePath, CancellationToken cancellationToken)
    {
        if (RemoveFailure is not null) throw RemoveFailure;
        RemovedTargets.Add((project.Id, worktreePath));
        return Task.CompletedTask;
    }
}
