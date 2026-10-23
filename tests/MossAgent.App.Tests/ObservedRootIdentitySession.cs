using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.Tests;

internal sealed class ObservedRootIdentitySession(IRootIdentitySession inner, Action disposed) : IRootIdentitySession
{
    private int _disposed;
    public IReadOnlyList<DirectoryObjectIdentity> Roots => inner.Roots;
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { inner.Dispose(); }
        finally { disposed(); }
    }
}
