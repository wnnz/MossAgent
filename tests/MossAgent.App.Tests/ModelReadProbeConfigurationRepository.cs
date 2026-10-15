using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class ModelReadProbeConfigurationRepository(
    IReadOnlyList<AiProvider> providers,
    IReadOnlyList<ModelProfile> models) : IConfigurationRepository
{
    public Func<Guid, Task>? BeforeModelReadAsync { get; set; }

    public Task<IReadOnlyList<ProxyProfile>> GetProxiesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProxyProfile>>([]);

    public Task SaveProxyAsync(ProxyProfile proxy, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task DeleteProxyAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<AiProvider>> GetProvidersAsync(CancellationToken cancellationToken) =>
        Task.FromResult(providers);

    public Task SaveProviderAsync(AiProvider provider, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task DeleteProviderAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public async Task<IReadOnlyList<ModelProfile>> GetModelsAsync(
        Guid providerId, CancellationToken cancellationToken)
    {
        if (BeforeModelReadAsync is { } beforeRead) await beforeRead(providerId);
        return models.Where(model => model.ProviderId == providerId).ToArray();
    }

    public Task SaveModelAsync(ModelProfile model, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task DeleteModelAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
