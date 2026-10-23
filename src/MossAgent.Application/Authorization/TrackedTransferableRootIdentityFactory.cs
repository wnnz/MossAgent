using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Application.Authorization;

internal sealed class TrackedTransferableRootIdentityFactory(
    ProjectRootSessionLifetime lifetime, ITransferableRootIdentitySessionFactory factory)
    : ITransferableRootIdentitySessionFactory
{
    public Task<IRootIdentitySession> OpenAsync(IReadOnlyList<string> roots, CancellationToken cancellationToken) =>
        lifetime.OpenAsync(factory, roots, cancellationToken);

    public Task<IRootIdentitySession> OpenTransferableAsync(IReadOnlyList<string> roots, CancellationToken cancellationToken) =>
        lifetime.OpenAsync(factory, roots, cancellationToken, transferable: true);
}
