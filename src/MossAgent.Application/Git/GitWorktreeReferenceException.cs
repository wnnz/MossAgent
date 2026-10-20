namespace MossAgent.Application.Git;

public sealed class GitWorktreeReferenceException() : Exception("The selected worktree base must resolve to a commit.");
