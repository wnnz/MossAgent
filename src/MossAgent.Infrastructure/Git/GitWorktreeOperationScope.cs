using MossAgent.Tools.Abstractions;

namespace MossAgent.Infrastructure.Git;

internal sealed class GitWorktreeOperationScope(
    CancellationTokenSource deadline, CancellationTokenSource operation,
    ITaskWorkspaceLease? projectLease, CancellationTokenSource scope) : IAsyncDisposable
{
    public CancellationToken Token { get; } = scope.Token;

    public Exception MapCancellation(OperationCanceledException failure, CancellationToken userToken, string verb) =>
        userToken.IsCancellationRequested
            ? new OperationCanceledException($"Git worktree {verb} was cancelled.", failure, userToken)
            : deadline.IsCancellationRequested
                ? new TimeoutException($"Git worktree {verb} timed out.", failure)
                : failure;

    public async ValueTask DisposeAsync()
    {
        scope.Dispose();
        if (projectLease is not null) await projectLease.DisposeAsync().ConfigureAwait(false);
        operation.Dispose();
        deadline.Dispose();
    }
}
