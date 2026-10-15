using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class ConfigurationMutationProbeRepository : IConfigurationRepository
{
    public ModelPickerProbeRepository Store { get; } = new();
    public Func<CancellationToken, Task>? BeforeWriteAsync { get; set; }
    public Func<CancellationToken, Task>? AfterWriteAsync { get; set; }
    public Func<Task<IReadOnlyList<ProxyProfile>>>? ProxyLoader { get; set; }
    public string? FailingOperation { get; set; }
    public List<string> Writes { get; } = [];

    public Task<IReadOnlyList<ProxyProfile>> GetProxiesAsync(CancellationToken token) => ProxyLoader?.Invoke() ?? Store.GetProxiesAsync(token);
    public Task SaveProxyAsync(ProxyProfile proxy, CancellationToken token) => throw new NotSupportedException();
    public Task DeleteProxyAsync(Guid id, CancellationToken token) => throw new NotSupportedException();
    public Task<IReadOnlyList<AiProvider>> GetProvidersAsync(CancellationToken token) => Store.GetProvidersAsync(token);
    public Task<IReadOnlyList<ModelProfile>> GetModelsAsync(Guid id, CancellationToken token) => Store.GetModelsAsync(id, token);

    public async Task SaveProviderAsync(AiProvider provider, CancellationToken token)
    {
        await BeforeMutationAsync("save-provider", token);
        await Store.SaveProviderAsync(provider, token);
        if (AfterWriteAsync is { } afterWrite) await afterWrite(token);
    }

    public async Task DeleteProviderAsync(Guid id, CancellationToken token)
    {
        await BeforeMutationAsync("delete-provider", token);
        await Store.DeleteProviderAsync(id, token);
    }

    public async Task SaveModelAsync(ModelProfile model, CancellationToken token)
    {
        await BeforeMutationAsync("save-model", token);
        await Store.SaveModelAsync(model, token);
        if (AfterWriteAsync is { } afterWrite) await afterWrite(token);
    }

    public async Task DeleteModelAsync(Guid id, CancellationToken token)
    {
        await BeforeMutationAsync("delete-model", token);
        await Store.DeleteModelAsync(id, token);
    }

    private async Task BeforeMutationAsync(string operation, CancellationToken token)
    {
        Writes.Add(operation);
        if (BeforeWriteAsync is { } beforeWrite) await beforeWrite(token);
        token.ThrowIfCancellationRequested();
        if (FailingOperation == operation) throw new IOException("fixture-secret-api-key-password");
    }
}
