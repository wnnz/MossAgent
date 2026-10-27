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

    /// <summary>Waits up to <paramref name="window"/> for in-flight revocation to land on the
    /// lease flag. Cancellation can reach <see cref="Token"/> through the caller side ahead of
    /// the lease's own revoke (e.g., change-event subscribers ordered before the monitor's), so
    /// a cancelled token with a not-yet-revoked lease gets a bounded attribution settle; returns
    /// the lease revocation state observed after the wait.</summary>
    public async Task<bool> AwaitRevocationAsync(TimeSpan window, TimeProvider timeProvider)
    {
        if (_lease is null || _lease.IsRevoked) return _lease?.IsRevoked == true;
        try { await Task.Delay(window, timeProvider, _lease.Revocation).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        return _lease.IsRevoked;
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
