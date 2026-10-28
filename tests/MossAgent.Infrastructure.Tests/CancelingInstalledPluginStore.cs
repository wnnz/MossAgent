using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Tests;

/// <summary>Cancels the supplied source when the name/version lookup runs, so the install
/// hits cancellation inside the extraction phase rather than during preview.</summary>
internal sealed class CancelingInstalledPluginStore(CancellationTokenSource source) : IInstalledPluginStore
{
    public Task<InstalledPlugin?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<InstalledPlugin?>(null);

    public Task<InstalledPlugin?> GetByNameVersionAsync(string name, string version,
        CancellationToken cancellationToken)
    {
        source.Cancel();
        return Task.FromResult<InstalledPlugin?>(null);
    }

    public Task<IReadOnlyList<InstalledPlugin>> ListByNameAsync(string name,
        CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<InstalledPlugin>>([]);

    public Task<InstalledPlugin> InsertAsync(InstalledPlugin plugin,
        IReadOnlyDictionary<string, string> payloadHashes, CancellationToken cancellationToken) =>
        Task.FromResult(plugin);

    public Task<InstalledPlugin> ReplaceByNameAsync(InstalledPlugin plugin,
        IReadOnlyDictionary<string, string> payloadHashes, CancellationToken cancellationToken) =>
        Task.FromResult(plugin);

    public Task<InstalledPlugin?> SetStateAsync(Guid id, InstalledPluginState state,
        CancellationToken cancellationToken) => Task.FromResult<InstalledPlugin?>(null);

    public Task<InstalledPlugin?> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<InstalledPlugin?>(null);

    public Task<IReadOnlyList<InstalledPlugin>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<InstalledPlugin>>([]);

    public Task<IReadOnlyDictionary<string, string>> ListPayloadHashesAsync(Guid pluginId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

    public Task SavePayloadHashesAsync(Guid pluginId, IReadOnlyDictionary<string, string> hashes,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
