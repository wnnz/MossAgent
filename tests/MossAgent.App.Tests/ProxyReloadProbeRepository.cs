using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class ProxyReloadProbeRepository : IConfigurationRepository
{
    public List<ProxyProfile> Proxies { get; } = [];
    public List<AiProvider> Providers { get; } = [];
    public Func<Task<IReadOnlyList<ProxyProfile>>>? ProxyLoader { get; set; }
    public CancellationToken ProxyReadToken { get; private set; }

    public Task<IReadOnlyList<ProxyProfile>> GetProxiesAsync(CancellationToken cancellationToken)
    {
        ProxyReadToken = cancellationToken;
        return ProxyLoader?.Invoke() ?? Task.FromResult<IReadOnlyList<ProxyProfile>>(Proxies.ToArray());
    }

    public Task SaveProxyAsync(ProxyProfile proxy, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task DeleteProxyAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<AiProvider>> GetProvidersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AiProvider>>(Providers.ToArray());

    public Task SaveProviderAsync(AiProvider provider, CancellationToken cancellationToken)
    {
        Providers.RemoveAll(item => item.Id == provider.Id);
        Providers.Add(provider);
        return Task.CompletedTask;
    }

    public Task DeleteProviderAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ModelProfile>> GetModelsAsync(Guid providerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ModelProfile>>([]);

    public Task SaveModelAsync(ModelProfile model, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task DeleteModelAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
