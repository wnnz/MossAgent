namespace MossAgent.Application.Git;

public sealed class WorktreeAuthorizationException() : UnauthorizedAccessException(
    "The managed worktree is unconfirmed or its directory object changed. Confirm a legacy worktree explicitly; restore the original object or create a new task when an enrolled object is lost.");
