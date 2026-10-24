using MossAgent.Tools.Abstractions;

namespace MossAgent.Infrastructure.Tests;

internal sealed class FaultingEnvironmentGuard : ITaskWorkspaceGuard, ITaskWorkspaceLeaseProvider, ITaskWorkspaceLease
{
    private readonly CancellationTokenSource _revocation = new();
    public Exception? DisposeFailure { get; init; }
    public bool Disposed { get; private set; }
    public Guid ProjectId { get; set; }
    public long Revision { get; set; }
    public CancellationToken Revocation => _revocation.Token;
    public bool IsRevoked => false;
    public Task ValidateAsync(ToolExecutionContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<ITaskWorkspaceLease> AcquireAsync(ToolExecutionContext context, CancellationToken cancellationToken)
        => Task.FromResult<ITaskWorkspaceLease>(this);
    public ValueTask DisposeAsync()
    {
        Disposed = true;
        _revocation.Dispose();
        return DisposeFailure is { } failure ? ValueTask.FromException(failure) : ValueTask.CompletedTask;
    }
}
