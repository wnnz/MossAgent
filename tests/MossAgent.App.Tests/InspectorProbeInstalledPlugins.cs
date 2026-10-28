using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.App.Tests;

/// <summary>Probe for the audit pane's best-effort current-plugin lookup; only
/// <see cref="GetAsync"/> is exercised — the write members are unreachable there.</summary>
internal sealed class InspectorProbeInstalledPlugins : IInstalledPluginStore
{
    public Func<Guid, CancellationToken, Task<InstalledPlugin?>> Lookups { get; set; } =
        (_, _) => Task.FromResult<InstalledPlugin?>(null);
    public List<Guid> Queried { get; } = [];

    public Task<InstalledPlugin?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Queried.Add(id);
        return Lookups(id, cancellationToken);
    }

    public Task<InstalledPlugin?> GetByNameVersionAsync(string name, string version,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<InstalledPlugin>> ListByNameAsync(string name,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<InstalledPlugin> InsertAsync(InstalledPlugin plugin,
        IReadOnlyDictionary<string, string> payloadHashes, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<InstalledPlugin> ReplaceByNameAsync(InstalledPlugin plugin,
        IReadOnlyDictionary<string, string> payloadHashes, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<InstalledPlugin?> SetStateAsync(Guid id, InstalledPluginState state,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<InstalledPlugin?> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<InstalledPlugin>> ListAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyDictionary<string, string>> ListPayloadHashesAsync(Guid pluginId,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task SavePayloadHashesAsync(Guid pluginId, IReadOnlyDictionary<string, string> hashes,
        CancellationToken cancellationToken) => throw new NotSupportedException();
}
