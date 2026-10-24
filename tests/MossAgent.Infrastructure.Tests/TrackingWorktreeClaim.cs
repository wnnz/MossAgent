using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class TrackingWorktreeClaim(string path, bool failVerify) : IWorktreeTargetClaim
{
    public string Path { get; } = path;
    public DirectoryObjectIdentity Identity { get; } =
        new(path, "VOLUME{TEST}", new string('0', 32));
    public int Verifications { get; private set; }
    public bool Disposed { get; private set; }

    public void Verify()
    {
        Verifications++;
        if (failVerify) throw new ProjectRootAuthorizationException();
    }

    public void Dispose() => Disposed = true;
}
