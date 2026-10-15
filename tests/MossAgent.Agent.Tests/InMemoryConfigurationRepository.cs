using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Agent.Tests;

internal sealed class InMemoryConfigurationRepository(
    AiProvider provider,
    ModelProfile model) : IConfigurationRepository
{
    public Task<IReadOnlyList<ProxyProfile>> GetProxiesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProxyProfile>>([]);

    public Task SaveProxyAsync(ProxyProfile proxy, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DeleteProxyAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<AiProvider>> GetProvidersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AiProvider>>([provider]);

    public Task SaveProviderAsync(AiProvider value, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DeleteProviderAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<ModelProfile>> GetModelsAsync(Guid providerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ModelProfile>>(providerId == provider.Id ? [model] : []);

    public Task SaveModelAsync(ModelProfile value, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DeleteModelAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
}
