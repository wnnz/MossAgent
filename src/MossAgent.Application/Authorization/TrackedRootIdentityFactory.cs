using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Application.Authorization;

internal sealed class TrackedRootIdentityFactory(
    ProjectRootSessionLifetime lifetime, IRootIdentitySessionFactory factory) : IRootIdentitySessionFactory
{
    public Task<IRootIdentitySession> OpenAsync(IReadOnlyList<string> roots, CancellationToken cancellationToken) =>
        lifetime.OpenAsync(factory, roots, cancellationToken);
}
