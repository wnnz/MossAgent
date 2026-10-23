using System.Runtime.ExceptionServices;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Application.Authorization;

internal sealed class TrackedPathQuarantineSession(IPathQuarantineSession session,
    Action<Exception?> released) : IPathQuarantineSession
{
    private int _disposing;
    private ExceptionDispatchInfo? _failure;
    public bool Moved => session.Moved;

    public Task MoveAsync(Func<CancellationToken, Task> validateAuthorization, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposing) != 0, this);
        return session.MoveAsync(validateAuthorization, cancellationToken);
    }

    public Task DeleteMovedAsync(Func<CancellationToken, Task> validateAuthorization, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposing) != 0, this);
        return session.DeleteMovedAsync(validateAuthorization, cancellationToken);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposing, 1) != 0) { _failure?.Throw(); return; }
        try { session.Dispose(); }
        catch (Exception failure)
        {
            _failure = ExceptionDispatchInfo.Capture(failure);
            released(failure);
            throw;
        }
        released(null);
    }
}
