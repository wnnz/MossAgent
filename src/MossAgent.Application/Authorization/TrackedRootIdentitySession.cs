using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Application.Authorization;

internal sealed class TrackedRootIdentitySession(Action<TrackedRootIdentitySession> released) : IRootIdentitySession
{
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task<IRootIdentitySession>? _pending;
    private int _disposing;
    private readonly Lock _sync = new();
    private int _transfers;
    private bool _rootExited;
    private bool _finished;
    private Exception? _cleanupFailure;
    public IReadOnlyList<DirectoryObjectIdentity> Roots { get; private set; } = [];
    internal Task Released => _released.Task;

    internal async Task<IRootIdentitySession> OpenAsync(IRootIdentitySessionFactory factory,
        IReadOnlyList<string> roots, CancellationToken cancellationToken, bool transferable)
    {
        try
        {
            _pending = transferable && factory is ITransferableRootIdentitySessionFactory transfers
                ? transfers.OpenTransferableAsync(roots, cancellationToken)
                : factory.OpenAsync(roots, cancellationToken);
            var session = await _pending.WaitAsync(cancellationToken).ConfigureAwait(false);
            Roots = session.Roots.ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            return session is IPathQuarantineSessionFactory quarantine
                ? new TrackedQuarantineRootIdentitySession(this, quarantine) : this;
        }
        catch
        {
            try { Dispose(); }
            catch (Exception) { /* The owning lease observes cleanup failure through Released. */ }
            throw;
        }
    }

    internal IPathQuarantineSession Transfer(IPathQuarantineSessionFactory factory, string source,
        string destination, IReadOnlyList<string> roots, CancellationToken token)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposing != 0, this);
            _transfers++;
        }
        try
        {
            return new TrackedPathQuarantineSession(factory.Create(source, destination, roots, token),
                failure => Complete(failure, rootExited: false));
        }
        catch { Complete(null, rootExited: false); throw; }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposing, 1) != 0) return;
        if (_pending is { IsCompleted: false } pending)
        {
            _ = DisposeLateAsync(pending);
            return;
        }
        try
        {
            if (_pending is { IsCompletedSuccessfully: true }) _pending.Result.Dispose();
            Complete(null);
        }
        catch (Exception exception) { Complete(exception); throw; }
    }

    private async Task DisposeLateAsync(Task<IRootIdentitySession> pending)
    {
        IRootIdentitySession session;
        try { session = await pending.ConfigureAwait(false); }
        catch (Exception) { Complete(null); return; }
        try { session.Dispose(); Complete(null); }
        catch (Exception exception) { Complete(exception); }
    }

    private void Complete(Exception? failure, bool rootExited = true)
    {
        Exception? cleanupFailure;
        lock (_sync)
        {
            if (rootExited) _rootExited = true;
            else _transfers--;
            _cleanupFailure ??= failure;
            if (!_rootExited || _transfers > 0 || _finished) return;
            _finished = true;
            cleanupFailure = _cleanupFailure;
        }
        if (cleanupFailure is not null) { _released.TrySetException(cleanupFailure); return; }
        released(this);
        _released.TrySetResult();
    }
}
