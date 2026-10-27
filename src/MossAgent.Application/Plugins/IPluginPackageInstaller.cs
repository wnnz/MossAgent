using MossAgent.Domain.Plugins;

namespace MossAgent.Application.Plugins;

/// <summary>Installs a local plugin package to a content-addressed directory and records the
/// row. Installation re-runs the full package validation itself and never trusts a preview the
/// caller obtained earlier. Fail-closed: rejections throw <see cref="PluginPackageException"/>
/// with a stable code and cancellation propagates unwrapped.</summary>
public interface IPluginPackageInstaller
{
    Task<InstalledPlugin> InstallAsync(string packagePath, CancellationToken cancellationToken);

    Task<InstalledPlugin?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<InstalledPlugin>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Clears interrupted-install staging trees and reports final directories that
    /// have no row. Orphaned payloads belong to the user and are only reported, never deleted
    /// or executed.</summary>
    Task<PluginInstallReconciliation> ReconcileAsync(CancellationToken cancellationToken);
}
