using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Browser.Tests;

internal sealed class StubConfigurationRepository : IConfigurationRepository
{
    public Task<IReadOnlyList<ProxyProfile>> GetProxiesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProxyProfile>>([]);

    public Task SaveProxyAsync(ProxyProfile proxy, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task DeleteProxyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<AiProvider>> GetProvidersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AiProvider>>([]);

    public Task SaveProviderAsync(AiProvider provider, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task DeleteProviderAsync(Guid id, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<ModelProfile>> GetModelsAsync(
        Guid providerId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ModelProfile>>([]);

    public Task SaveModelAsync(ModelProfile model, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task DeleteModelAsync(Guid id, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
