using MossAgent.Domain.Plugins;

namespace MossAgent.Application.Plugins;

/// <summary>Persists installed plugin rows as small metadata only; archive payloads stay on
/// disk. Insert is compare-and-swap on (name, version): a row with the same content hash is an
/// idempotent success returning the stored row, a row with a different hash throws
/// <see cref="PluginPackageException"/> with <see cref="PluginPackageErrorCode.InstallConflict"/>.</summary>
public interface IInstalledPluginStore
{
    Task<InstalledPlugin?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<InstalledPlugin?> GetByNameVersionAsync(string name, string version, CancellationToken cancellationToken);

    Task<InstalledPlugin> InsertAsync(InstalledPlugin plugin, CancellationToken cancellationToken);

    Task<IReadOnlyList<InstalledPlugin>> ListAsync(CancellationToken cancellationToken);
}
