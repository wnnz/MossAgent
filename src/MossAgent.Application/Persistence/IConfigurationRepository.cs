using MossAgent.Domain;

namespace MossAgent.Application.Persistence;

public interface IConfigurationRepository
{
    Task<IReadOnlyList<ProxyProfile>> GetProxiesAsync(CancellationToken cancellationToken);
    Task SaveProxyAsync(ProxyProfile proxy, CancellationToken cancellationToken);
    Task DeleteProxyAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AiProvider>> GetProvidersAsync(CancellationToken cancellationToken);
    Task SaveProviderAsync(AiProvider provider, CancellationToken cancellationToken);
    Task DeleteProviderAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelProfile>> GetModelsAsync(Guid providerId, CancellationToken cancellationToken);
    Task SaveModelAsync(ModelProfile model, CancellationToken cancellationToken);
    Task DeleteModelAsync(Guid id, CancellationToken cancellationToken);
}

