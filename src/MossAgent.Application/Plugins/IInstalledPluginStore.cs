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

    /// <summary>Single-statement state transition. Lifecycle transitions are total and
    /// idempotent, so no expected-state check is needed; returns the updated row or null
    /// when the id no longer exists.</summary>
    Task<InstalledPlugin?> SetStateAsync(Guid id, InstalledPluginState state, CancellationToken cancellationToken);

    /// <summary>Removes the row and returns it as stored, or null when the id no longer
    /// exists. The caller owns any payload cleanup after the row is gone.</summary>
    Task<InstalledPlugin?> DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<InstalledPlugin>> ListAsync(CancellationToken cancellationToken);
}
