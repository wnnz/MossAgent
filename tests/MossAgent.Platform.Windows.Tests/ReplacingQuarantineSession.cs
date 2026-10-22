using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class ReplacingQuarantineSession(
    IPathQuarantineSession inner,
    Action afterMove,
    Exception? deleteFailure) : IPathQuarantineSession
{
    public bool Moved => inner.Moved;

    public async Task MoveAsync(Func<CancellationToken, Task> validateAuthorization,
        CancellationToken cancellationToken)
    {
        await inner.MoveAsync(validateAuthorization, cancellationToken);
        afterMove();
    }

    public Task DeleteMovedAsync(Func<CancellationToken, Task> validateAuthorization,
        CancellationToken cancellationToken) => deleteFailure is null
        ? inner.DeleteMovedAsync(validateAuthorization, cancellationToken)
        : Task.FromException(deleteFailure);

    public void Dispose() => inner.Dispose();
}
