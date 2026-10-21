using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class RootIdentityProbeSession(IReadOnlyList<DirectoryObjectIdentity> roots, Action released) : IRootIdentitySession
{
    private int _disposed;
    public IReadOnlyList<DirectoryObjectIdentity> Roots { get; } = roots.ToArray();
    public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        released();
        Disposed.TrySetResult();
    }
}
