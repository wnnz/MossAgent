using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Tests;

/// <summary>Returns a fixed plugin list for activator tests; lookups consult the same list.
/// Payload hashes mirror the SQLite baseline: seed them through the constructor or
/// <see cref="InsertAsync"/>, writes only ever insert missing keys, and state transitions are
/// recorded on <see cref="StateChanges"/> without mutating the fixed rows.</summary>
internal sealed class StaticInstalledPluginStore(IReadOnlyList<InstalledPlugin> rows,
    IReadOnlyDictionary<Guid, Dictionary<string, string>>? hashes = null) : IInstalledPluginStore
{
    private readonly Dictionary<Guid, Dictionary<string, string>> _hashes =
        hashes?.ToDictionary(pair => pair.Key,
            pair => new Dictionary<string, string>(pair.Value, StringComparer.OrdinalIgnoreCase)) ?? [];

    public List<(Guid Id, InstalledPluginState State)> StateChanges { get; } = [];

    public Task<InstalledPlugin?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(rows.FirstOrDefault(plugin => plugin.Id == id));

    public Task<InstalledPlugin?> GetByNameVersionAsync(string name, string version,
        CancellationToken cancellationToken) =>
        Task.FromResult(rows.FirstOrDefault(plugin => plugin.Name == name && plugin.Version == version));

    public Task<InstalledPlugin> InsertAsync(InstalledPlugin plugin,
        IReadOnlyDictionary<string, string> payloadHashes, CancellationToken cancellationToken)
    {
        SaveHashes(plugin.Id, payloadHashes);
        return Task.FromResult(plugin);
    }

    public Task<InstalledPlugin?> SetStateAsync(Guid id, InstalledPluginState state,
        CancellationToken cancellationToken)
    {
        StateChanges.Add((id, state));
        return Task.FromResult(rows.FirstOrDefault(plugin => plugin.Id == id) is { } row
            ? row with { State = state } : null);
    }

    public Task<InstalledPlugin?> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(rows.FirstOrDefault(plugin => plugin.Id == id));

    public Task<IReadOnlyList<InstalledPlugin>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult(rows);

    public Task<IReadOnlyDictionary<string, string>> ListPayloadHashesAsync(Guid pluginId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(
            _hashes.TryGetValue(pluginId, out var stored)
                ? new Dictionary<string, string>(stored, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    public Task SavePayloadHashesAsync(Guid pluginId, IReadOnlyDictionary<string, string> hashes,
        CancellationToken cancellationToken)
    {
        SaveHashes(pluginId, hashes);
        return Task.CompletedTask;
    }

    private void SaveHashes(Guid pluginId, IReadOnlyDictionary<string, string> hashes)
    {
        if (!_hashes.TryGetValue(pluginId, out var stored))
            stored = _hashes[pluginId] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, hash) in hashes)
            stored.TryAdd(path, hash);
    }
}
