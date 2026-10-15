using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

public sealed class FixedModelDiscoveryService(IReadOnlyList<DiscoveredModel> models)
    : IModelDiscoveryService
{
    public AiProvider? LastRequestedProvider { get; private set; }

    public Task<IReadOnlyList<DiscoveredModel>> DiscoverAsync(
        AiProvider provider,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastRequestedProvider = provider;
        return Task.FromResult(models);
    }
}
