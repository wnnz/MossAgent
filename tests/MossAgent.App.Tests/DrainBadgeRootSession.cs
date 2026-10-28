using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.Tests;

internal sealed class DrainBadgeRootSession(IReadOnlyList<DirectoryObjectIdentity> roots,
    Exception? failure, int failAttempts = int.MaxValue) : IRootIdentitySession
{
    private int _attempts;

    public IReadOnlyList<DirectoryObjectIdentity> Roots { get; } = roots;

    public int Attempts => Volatile.Read(ref _attempts);

    public void Dispose()
    {
        var attempt = Interlocked.Increment(ref _attempts);
        if (failure is not null && attempt <= failAttempts) throw failure;
    }
}
