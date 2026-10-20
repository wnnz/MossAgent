namespace MossAgent.Application.Git;

public sealed class GitWorktreeCreationException(string worktreePath, Exception innerException)
    : Exception("Worktree creation stopped with a retained managed directory.", innerException)
{
    public string WorktreePath { get; } = worktreePath;
}
