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

    public Task<InstalledPlugin> InsertAsync(InstalledPlugin plugin, CancellationToken cancellationToken) =>
        Task.FromResult(plugin);

    public Task<InstalledPlugin?> SetStateAsync(Guid id, InstalledPluginState state,
        CancellationToken cancellationToken) => Task.FromResult<InstalledPlugin?>(null);

    public Task<InstalledPlugin?> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<InstalledPlugin?>(null);

    public Task<IReadOnlyList<InstalledPlugin>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<InstalledPlugin>>([]);
}
