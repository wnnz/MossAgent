using MossAgent.Domain.Plugins;

namespace MossAgent.Application.Plugins;

/// <summary>Persists installed plugin rows as small metadata only; archive payloads stay on
/// disk. Insert is compare-and-swap on (name, version): a row with the same content hash is an
/// idempotent success returning the stored row, a row with a different hash throws
/// <see cref="PluginPackageException"/> with <see cref="PluginPackageErrorCode.InstallConflict"/>.
/// Each row also owns a per-component payload-hash baseline recorded at install time; dispatch
/// verifies on-disk bytes against it and marks the row Drifted on any missing entry or
/// mismatch.</summary>
public interface IInstalledPluginStore
{
    Task<InstalledPlugin?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<InstalledPlugin?> GetByNameVersionAsync(string name, string version, CancellationToken cancellationToken);

    /// <summary>Writes the plugin row and its component payload hashes in one transaction.
    /// <paramref name="payloadHashes"/> is the install-time SHA-256 baseline keyed by normalized
    /// component path; an idempotent reinstall of identical content keeps the stored row and
    /// only fills baseline entries that are still missing.</summary>
    Task<InstalledPlugin> InsertAsync(InstalledPlugin plugin,
        IReadOnlyDictionary<string, string> payloadHashes, CancellationToken cancellationToken);

    /// <summary>Single-statement state transition. Lifecycle transitions are total and
    /// idempotent, so no expected-state check is needed; Drifted is reachable from any state.
    /// Enablement of a drifted row is refused at the installer layer, not here. Returns the
    /// updated row or null when the id no longer exists.</summary>
    Task<InstalledPlugin?> SetStateAsync(Guid id, InstalledPluginState state, CancellationToken cancellationToken);

    /// <summary>Removes the row and returns it as stored, or null when the id no longer
    /// exists. Payload hash rows cascade away with the row; the caller owns any payload
    /// cleanup after the row is gone.</summary>
    Task<InstalledPlugin?> DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<InstalledPlugin>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Returns the stored payload-hash baseline for one plugin, keyed by component
    /// path; an empty map means no baseline was recorded (pre-baseline rows included).</summary>
    Task<IReadOnlyDictionary<string, string>> ListPayloadHashesAsync(Guid pluginId,
        CancellationToken cancellationToken);

    /// <summary>Adds baseline hash rows for component paths that have none yet. Existing keys
    /// are never overwritten — an established baseline stays what was recorded first.</summary>
    Task SavePayloadHashesAsync(Guid pluginId, IReadOnlyDictionary<string, string> hashes,
        CancellationToken cancellationToken);
}
