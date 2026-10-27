using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.Tests;

internal sealed class RevocableWorkspaceLease : ITaskWorkspaceGuard, ITaskWorkspaceLeaseProvider, ITaskWorkspaceLease
{
    private readonly CancellationTokenSource _revocation = new();
    private int _revoked;

    public Guid ProjectId { get; } = Guid.NewGuid();
    public long Revision => 1;
    public CancellationToken Revocation => _revocation.Token;
    public bool IsRevoked => Volatile.Read(ref _revoked) != 0;
    public bool Disposed { get; private set; }

    public Task ValidateAsync(ToolExecutionContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<ITaskWorkspaceLease> AcquireAsync(ToolExecutionContext context, CancellationToken cancellationToken) =>
        Task.FromResult<ITaskWorkspaceLease>(this);

    public void Revoke()
    {
        if (Interlocked.Exchange(ref _revoked, 1) != 0) return;
        _revocation.Cancel();
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        _revocation.Dispose();
        return ValueTask.CompletedTask;
    }
}
