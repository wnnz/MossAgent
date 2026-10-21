using MossAgent.Domain;

namespace MossAgent.Tools.Abstractions;

public sealed class TaskWorkspaceOperation : IAsyncDisposable
{
    private readonly ITaskWorkspaceLease? _lease;
    private readonly CancellationTokenSource? _linked;

    private TaskWorkspaceOperation(ITaskWorkspaceLease? lease, CancellationToken cancellationToken)
    {
        _lease = lease;
        if (lease is not null) _linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.Revocation);
        Token = _linked?.Token ?? cancellationToken;
    }

    public CancellationToken Token { get; }
    public bool IsRevoked => _lease?.IsRevoked == true;

    public static async Task<TaskWorkspaceOperation> EnterAsync(ToolExecutionContext context, CancellationToken cancellationToken)
    {
        var lease = context.WorkspaceGuard is ITaskWorkspaceLeaseProvider provider
            ? await provider.AcquireAsync(context, cancellationToken).ConfigureAwait(false) : null;
        try { return new(lease, cancellationToken); }
        catch { if (lease is not null) await lease.DisposeAsync().ConfigureAwait(false); throw; }
    }

    public void ThrowIfRevoked()
    {
        if (IsRevoked) throw new WorkspaceAuthorizationException();
    }

    public async ValueTask DisposeAsync()
    {
        try { _linked?.Dispose(); }
        finally { if (_lease is not null) await _lease.DisposeAsync().ConfigureAwait(false); }
    }
}
