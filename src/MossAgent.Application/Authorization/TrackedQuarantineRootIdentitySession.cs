using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Application.Authorization;

internal sealed class TrackedQuarantineRootIdentitySession(
    TrackedRootIdentitySession session, IPathQuarantineSessionFactory quarantine)
    : IRootIdentitySession, IPathQuarantineSessionFactory
{
    public IReadOnlyList<DirectoryObjectIdentity> Roots => session.Roots;

    public IPathQuarantineSession Create(string source, string destination,
        IReadOnlyList<string> authorizedRoots, CancellationToken cancellationToken) =>
        session.Transfer(quarantine, source, destination, authorizedRoots, cancellationToken);

    public void Dispose() => session.Dispose();
}
