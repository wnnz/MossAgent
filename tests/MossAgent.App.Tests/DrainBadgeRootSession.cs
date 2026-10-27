using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.Tests;

internal sealed class DrainBadgeRootSession(IReadOnlyList<DirectoryObjectIdentity> roots, Exception? failure)
    : IRootIdentitySession
{
    public IReadOnlyList<DirectoryObjectIdentity> Roots { get; } = roots;

    public void Dispose()
    {
        if (failure is not null) throw failure;
    }
}
