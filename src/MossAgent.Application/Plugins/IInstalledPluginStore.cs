using MossAgent.Domain.Plugins;

namespace MossAgent.Application.Plugins;

/// <summary>Persists installed plugin rows as small metadata only; archive payloads stay on
/// disk. Insert is compare-and-swap on (name, version): a row with the same content hash is an
/// idempotent success returning the stored row, a row with a different hash throws
/// <see cref="PluginPackageException"/> with <see cref="PluginPackageErrorCode.InstallConflict"/>.
/// A different version under an existing name is an upgrade: <see cref="ReplaceByNameAsync"/>
/// atomically removes every same-name row and commits the new row, so at most one version of a
/// name is installed and two enabled same-name rows can never coexist. Each row also owns a
/// per-component payload-hash baseline recorded at install time; dispatch verifies on-disk
/// bytes against it and marks the row Drifted on any missing entry or mismatch. Every row
/// carries a store-owned trust_revision: inserted and replaced rows start at 1 and
/// <see cref="SetStateAsync"/> increments it on each real state transition, so the revision
/// identifies the exact trust generation a dispatch ran under.</summary>
public interface IInstalledPluginStore
{
    Task<InstalledPlugin?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<InstalledPlugin?> GetByNameVersionAsync(string name, string version, CancellationToken cancellationToken);

    /// <summary>Returns every row sharing <paramref name="name"/> across versions, ordered by
    /// version; an empty list means no install of that name exists.</summary>
    Task<IReadOnlyList<InstalledPlugin>> ListByNameAsync(string name, CancellationToken cancellationToken);

    /// <summary>Writes the plugin row and its component payload hashes in one transaction.
    /// <paramref name="payloadHashes"/> is the install-time SHA-256 baseline keyed by normalized
    /// component path; an idempotent reinstall of identical content keeps the stored row and
    /// only fills baseline entries that are still missing.</summary>
    Task<InstalledPlugin> InsertAsync(InstalledPlugin plugin,
        IReadOnlyDictionary<string, string> payloadHashes, CancellationToken cancellationToken);

    /// <summary>Atomic upgrade-replace: in one transaction deletes every row sharing
    /// <see cref="InstalledPlugin.Name"/> (any version), then inserts the new row with its
    /// complete hash baseline. Any failure rolls the whole replace back, so the replaced rows
    /// are either fully swapped or fully kept — never partially removed. Callers route the
    /// same (name, version) key through <see cref="InsertAsync"/> instead: this method replaces
    /// by name unconditionally. The new row's state is whatever <paramref name="plugin"/>
    /// carries — install always supplies <see cref="InstalledPluginState.Installed"/> so a
    /// replaced payload must be re-trusted by an explicit enable, never inheriting the old
    /// row's trust; the new row's trust revision likewise resets to 1. The caller owns
    /// payload-directory cleanup after commit.</summary>
    Task<InstalledPlugin> ReplaceByNameAsync(InstalledPlugin plugin,
        IReadOnlyDictionary<string, string> payloadHashes, CancellationToken cancellationToken);

    /// <summary>Single-statement state transition. Lifecycle transitions are total and
    /// idempotent, so no expected-state check is needed; Drifted is reachable from any state.
    /// Enablement of a drifted row is refused at the installer layer, not here. A real
    /// transition also increments the row's trust revision; a same-state call is a no-op that
    /// leaves the revision untouched. Returns the updated row or null when the id no longer
    /// exists.</summary>
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
