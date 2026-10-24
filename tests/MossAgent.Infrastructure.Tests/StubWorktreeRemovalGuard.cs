using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class StubWorktreeRemovalGuard(string path, DirectoryObjectIdentity expected,
    Exception? confirmFailure = null) : IWorktreeRemovalGuard
{
    public string Path { get; } = path;
    public DirectoryObjectIdentity Expected { get; } = expected;
    public int Confirmations { get; private set; }
    public bool Disposed { get; private set; }

    public void ConfirmRemoved()
    {
        Confirmations++;
        if (confirmFailure is not null) throw confirmFailure;
    }

    public void Dispose() => Disposed = true;
}
