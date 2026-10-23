using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class QuarantineRootProbe(DirectoryObjectIdentity identity, IPathQuarantineSession quarantine, bool rejectCreation = false)
    : IRootIdentitySession, IPathQuarantineSessionFactory
{
    public IReadOnlyList<DirectoryObjectIdentity> Roots { get; } = [identity];
    public IPathQuarantineSession Create(string source, string destination,
        IReadOnlyList<string> authorizedRoots, CancellationToken cancellationToken) =>
        rejectCreation ? throw new UnauthorizedAccessException("Synthetic transfer refusal.") : quarantine;
    public void Dispose() { }
}
