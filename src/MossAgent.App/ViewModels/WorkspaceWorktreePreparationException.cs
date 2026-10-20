namespace MossAgent.App.ViewModels;

public sealed class WorkspaceWorktreePreparationException(string worktreePath, Exception innerException)
    : Exception("Worktree preparation requires explicit recovery.", innerException)
{
    public string WorktreePath { get; } = worktreePath;
}
