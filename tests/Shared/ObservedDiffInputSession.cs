using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Testing;

internal sealed class ObservedDiffInputSession(IDiffInputFileSession inner) : IDiffInputFileSession
{
    private int _disposed;
    public string Path => inner.Path;
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
    public Action? Releasing { get; set; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { Releasing?.Invoke(); }
        finally { inner.Dispose(); }
    }
}
