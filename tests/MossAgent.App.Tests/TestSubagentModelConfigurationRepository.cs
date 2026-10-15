using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class TestSubagentModelConfigurationRepository(
    AiProvider provider,
    ModelProfile model) : IConfigurationRepository
{
    public Func<Task<IReadOnlyList<ModelProfile>>>? ModelLoader { get; set; }

    public Task<IReadOnlyList<ProxyProfile>> GetProxiesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProxyProfile>>([]);
    public Task SaveProxyAsync(ProxyProfile proxy, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DeleteProxyAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<IReadOnlyList<AiProvider>> GetProvidersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AiProvider>>([provider]);
    public Task SaveProviderAsync(AiProvider value, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DeleteProviderAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<IReadOnlyList<ModelProfile>> GetModelsAsync(Guid providerId, CancellationToken cancellationToken) =>
        ModelLoader?.Invoke() ?? Task.FromResult<IReadOnlyList<ModelProfile>>(providerId == provider.Id ? [model] : []);
    public Task SaveModelAsync(ModelProfile value, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DeleteModelAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
}
