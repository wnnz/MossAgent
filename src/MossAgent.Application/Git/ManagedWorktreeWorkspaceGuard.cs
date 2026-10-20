using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Application.Git;

public sealed class ManagedWorktreeWorkspaceGuard(
    IGitWorktreeService worktrees, ProjectProfile project, Guid ownerTaskId, string worktreePath,
    IReadOnlyList<string> authorizedRoots) : ITaskWorkspaceGuard
{
    private readonly ProjectProfile _project = project with { AuthorizedDirectories = project.AuthorizedDirectories.ToArray() };
    private readonly string[] _roots = authorizedRoots.ToArray();
    private static StringComparer Comparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public async Task ValidateAsync(ToolExecutionContext context, CancellationToken cancellationToken)
    {
        ValidateContext(context);
        await worktrees.ValidateAsync(_project, ownerTaskId, worktreePath, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateContext(context);
    }

    private void ValidateContext(ToolExecutionContext context)
    {
        if (!Comparer.Equals(context.WorkingDirectory, worktreePath) || !_roots.SequenceEqual(context.AuthorizedRoots, Comparer))
            throw new UnauthorizedAccessException("The tool context no longer matches its managed workspace authorization.");
    }
}
