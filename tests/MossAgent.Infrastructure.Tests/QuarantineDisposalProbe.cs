using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class QuarantineDisposalProbe(bool fails) : IPathQuarantineSession
{
    public int Disposals { get; private set; }
    public bool Moved { get; private set; }
    public async Task MoveAsync(Func<CancellationToken, Task> validateAuthorization, CancellationToken cancellationToken)
    {
        await validateAuthorization(cancellationToken);
        Moved = true;
    }
    public Task DeleteMovedAsync(Func<CancellationToken, Task> validateAuthorization, CancellationToken cancellationToken) =>
        validateAuthorization(cancellationToken);
    public void Dispose()
    {
        Disposals++;
        if (fails) throw new InvalidOperationException("Synthetic transferred-root cleanup failure.");
    }
}
