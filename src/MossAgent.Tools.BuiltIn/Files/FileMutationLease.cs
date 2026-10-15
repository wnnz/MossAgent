namespace MossAgent.Tools.BuiltIn.Files;

internal sealed class FileMutationLease(SemaphoreSlim gate) : IDisposable
{
    private SemaphoreSlim? _gate = gate;

    public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
}
