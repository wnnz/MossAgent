using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Git;

internal static class GitWorktreeFailureCode
{
    public static string FromException(Exception exception) => exception switch
    {
        OperationCanceledException => "cancelled",
        TimeoutException => "timeout",
        UnauthorizedAccessException or WorkspaceAuthorizationException => "authorization",
        FileMutationSessionException => "unsafe_target",
        IOException => "target_unavailable",
        _ => "worktree_creation_failed"
    };

    public static string CleanupFromException(Exception exception) => exception switch
    {
        OperationCanceledException => "cancelled",
        TimeoutException => "timeout",
        UnauthorizedAccessException or WorkspaceAuthorizationException => "authorization",
        FileMutationSessionException => "unsafe_target",
        IOException => "target_unavailable",
        _ => "worktree_cleanup_failed"
    };
}
