namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceDraftWriteLease(SemaphoreSlim gate) : IDisposable
{
    public static async Task<WorkspaceDraftWriteLease> AcquireAsync(SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new(gate);
    }

    public void Dispose() => gate.Release();
}
