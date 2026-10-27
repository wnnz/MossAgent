using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Tests;

/// <summary>A store that reports no existing row and always fails the insert, to prove the
/// installer removes the moved payload when the row write fails.</summary>
internal sealed class FailingInstalledPluginStore(Exception error) : IInstalledPluginStore
{
    public Task<InstalledPlugin?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<InstalledPlugin?>(null);

    public Task<InstalledPlugin?> GetByNameVersionAsync(string name, string version,
        CancellationToken cancellationToken) => Task.FromResult<InstalledPlugin?>(null);

    public Task<InstalledPlugin> InsertAsync(InstalledPlugin plugin, CancellationToken cancellationToken) =>
        Task.FromException<InstalledPlugin>(error);

    public Task<InstalledPlugin?> SetStateAsync(Guid id, InstalledPluginState state,
        CancellationToken cancellationToken) => Task.FromResult<InstalledPlugin?>(null);

    public Task<InstalledPlugin?> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<InstalledPlugin?>(null);

    public Task<IReadOnlyList<InstalledPlugin>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<InstalledPlugin>>([]);
}
