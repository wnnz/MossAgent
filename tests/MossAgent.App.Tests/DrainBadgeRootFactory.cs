using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.Tests;

internal sealed class DrainBadgeRootFactory(IReadOnlyList<DirectoryObjectIdentity> roots) : IRootIdentitySessionFactory
{
    private int _opens;

    public Task<IRootIdentitySession> OpenAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failure = Interlocked.Increment(ref _opens) == 2
            ? new InvalidOperationException("synthetic drain badge release failure") : null;
        return Task.FromResult<IRootIdentitySession>(new DrainBadgeRootSession(roots, failure));
    }
}
