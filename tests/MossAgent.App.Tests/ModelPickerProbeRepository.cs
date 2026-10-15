using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class ModelPickerProbeRepository : IConfigurationRepository
{
    public List<AiProvider> Providers { get; } = [];
    public List<ModelProfile> Models { get; } = [];
    public Func<Task<IReadOnlyList<AiProvider>>>? ProviderLoader { get; set; }
    public Func<Guid, Task<IReadOnlyList<ModelProfile>>>? ModelLoader { get; set; }
    public CancellationToken ProviderReadToken { get; private set; }
    public CancellationToken ModelReadToken { get; private set; }
    public int ProviderReadCalls { get; private set; }

    public Task<IReadOnlyList<ProxyProfile>> GetProxiesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProxyProfile>>([]);
    public Task SaveProxyAsync(ProxyProfile proxy, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task DeleteProxyAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<AiProvider>> GetProvidersAsync(CancellationToken cancellationToken)
    {
        ProviderReadCalls++;
        ProviderReadToken = cancellationToken;
        return ProviderLoader?.Invoke() ?? Task.FromResult<IReadOnlyList<AiProvider>>(Providers.ToArray());
    }
    public Task SaveProviderAsync(AiProvider provider, CancellationToken cancellationToken)
    {
        Providers.RemoveAll(item => item.Id == provider.Id);
        Providers.Add(provider);
        return Task.CompletedTask;
    }
    public Task DeleteProviderAsync(Guid id, CancellationToken cancellationToken)
    {
        Providers.RemoveAll(item => item.Id == id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ModelProfile>> GetModelsAsync(Guid providerId, CancellationToken cancellationToken)
    {
        ModelReadToken = cancellationToken;
        return ModelLoader?.Invoke(providerId) ?? Task.FromResult<IReadOnlyList<ModelProfile>>(
            Models.Where(model => model.ProviderId == providerId).ToArray());
    }
    public Task SaveModelAsync(ModelProfile model, CancellationToken cancellationToken)
    {
        Models.RemoveAll(item => item.Id == model.Id);
        Models.Add(model);
        return Task.CompletedTask;
    }
    public Task DeleteModelAsync(Guid id, CancellationToken cancellationToken)
    {
        Models.RemoveAll(item => item.Id == id);
        return Task.CompletedTask;
    }
}
