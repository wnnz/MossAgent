using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Tests;

/// <summary>Returns a fixed plugin list for activator tests; lookups consult the same list and
/// mutation members are inert because activation never writes plugin rows.</summary>
internal sealed class StaticInstalledPluginStore(IReadOnlyList<InstalledPlugin> rows) : IInstalledPluginStore
{
    public Task<InstalledPlugin?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(rows.FirstOrDefault(plugin => plugin.Id == id));

    public Task<InstalledPlugin?> GetByNameVersionAsync(string name, string version,
        CancellationToken cancellationToken) =>
        Task.FromResult(rows.FirstOrDefault(plugin => plugin.Name == name && plugin.Version == version));

    public Task<InstalledPlugin> InsertAsync(InstalledPlugin plugin, CancellationToken cancellationToken) =>
        Task.FromResult(plugin);

    public Task<InstalledPlugin?> SetStateAsync(Guid id, InstalledPluginState state,
        CancellationToken cancellationToken) => Task.FromResult<InstalledPlugin?>(null);

    public Task<InstalledPlugin?> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<InstalledPlugin?>(null);

    public Task<IReadOnlyList<InstalledPlugin>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult(rows);
}
