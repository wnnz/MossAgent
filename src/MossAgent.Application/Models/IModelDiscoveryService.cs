using MossAgent.Domain;

namespace MossAgent.Application.Models;

public interface IModelDiscoveryService
{
    Task<IReadOnlyList<DiscoveredModel>> DiscoverAsync(
        AiProvider provider,
        CancellationToken cancellationToken);
}

