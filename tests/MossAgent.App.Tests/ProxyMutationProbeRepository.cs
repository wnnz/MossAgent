using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class ProxyMutationProbeRepository : IConfigurationRepository
{
    private readonly ModelPickerProbeRepository _catalog = new();
    public List<ProxyProfile> Proxies { get; } = [];
    public List<string> Writes { get; } = [];
    public Func<string, CancellationToken, Task>? BeforeWrite { get; set; }
    public Func<CancellationToken, Task<IReadOnlyList<ProxyProfile>>>? Loader { get; set; }
    public string? FailingOperation { get; set; }

    public Task<IReadOnlyList<ProxyProfile>> GetProxiesAsync(CancellationToken token) =>
        Loader?.Invoke(token) ?? Task.FromResult<IReadOnlyList<ProxyProfile>>(Proxies.ToArray());

    public async Task SaveProxyAsync(ProxyProfile proxy, CancellationToken token)
    {
        await BeforeWriteAsync("save", token);
        Proxies.RemoveAll(item => item.Id == proxy.Id);
        Proxies.Add(proxy);
    }

    public async Task DeleteProxyAsync(Guid id, CancellationToken token)
    {
        await BeforeWriteAsync("delete", token);
        Proxies.RemoveAll(item => item.Id == id);
    }

    private async Task BeforeWriteAsync(string operation, CancellationToken token)
    {
        Writes.Add(operation);
        if (BeforeWrite is { } before) await before(operation, token);
        token.ThrowIfCancellationRequested();
        if (FailingOperation == operation) throw new IOException("fixture-secret-proxy-password");
    }

    public Task<IReadOnlyList<AiProvider>> GetProvidersAsync(CancellationToken token) => _catalog.GetProvidersAsync(token);
    public Task SaveProviderAsync(AiProvider provider, CancellationToken token) => _catalog.SaveProviderAsync(provider, token);
    public Task DeleteProviderAsync(Guid id, CancellationToken token) => _catalog.DeleteProviderAsync(id, token);
    public Task<IReadOnlyList<ModelProfile>> GetModelsAsync(Guid id, CancellationToken token) => _catalog.GetModelsAsync(id, token);
    public Task SaveModelAsync(ModelProfile model, CancellationToken token) => _catalog.SaveModelAsync(model, token);
    public Task DeleteModelAsync(Guid id, CancellationToken token) => _catalog.DeleteModelAsync(id, token);
}
