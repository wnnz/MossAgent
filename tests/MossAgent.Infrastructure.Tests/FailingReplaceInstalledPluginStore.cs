using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Tests;

/// <summary>Delegates every read and write to the wrapped store except
/// <see cref="ReplaceByNameAsync"/>, which always fails — so an upgrade-replace dies inside
/// the store write while the replaced rows stay exactly as the inner store left them.</summary>
internal sealed class FailingReplaceInstalledPluginStore(IInstalledPluginStore inner,
    Exception error) : IInstalledPluginStore
{
    public int ReplaceCalls { get; private set; }

    public Task<InstalledPlugin?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        inner.GetAsync(id, cancellationToken);

    public Task<InstalledPlugin?> GetByNameVersionAsync(string name, string version,
        CancellationToken cancellationToken) =>
        inner.GetByNameVersionAsync(name, version, cancellationToken);

    public Task<IReadOnlyList<InstalledPlugin>> ListByNameAsync(string name,
        CancellationToken cancellationToken) => inner.ListByNameAsync(name, cancellationToken);

    public Task<InstalledPlugin> InsertAsync(InstalledPlugin plugin,
        IReadOnlyDictionary<string, string> payloadHashes, CancellationToken cancellationToken) =>
        inner.InsertAsync(plugin, payloadHashes, cancellationToken);

    public Task<InstalledPlugin> ReplaceByNameAsync(InstalledPlugin plugin,
        IReadOnlyDictionary<string, string> payloadHashes, CancellationToken cancellationToken)
    {
        ReplaceCalls++;
        return Task.FromException<InstalledPlugin>(error);
    }

    public Task<InstalledPlugin?> SetStateAsync(Guid id, InstalledPluginState state,
        CancellationToken cancellationToken) => inner.SetStateAsync(id, state, cancellationToken);

    public Task<InstalledPlugin?> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        inner.DeleteAsync(id, cancellationToken);

    public Task<IReadOnlyList<InstalledPlugin>> ListAsync(CancellationToken cancellationToken) =>
        inner.ListAsync(cancellationToken);

    public Task<IReadOnlyDictionary<string, string>> ListPayloadHashesAsync(Guid pluginId,
        CancellationToken cancellationToken) =>
        inner.ListPayloadHashesAsync(pluginId, cancellationToken);

    public Task SavePayloadHashesAsync(Guid pluginId, IReadOnlyDictionary<string, string> hashes,
        CancellationToken cancellationToken) =>
        inner.SavePayloadHashesAsync(pluginId, hashes, cancellationToken);
}
