using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.App.Tests;

internal sealed class RecordingPluginPackageInstaller : IPluginPackageInstaller
{
    public int ReconcileCalls { get; private set; }
    public Exception? ReconcileFailure { get; set; }
    public PluginInstallReconciliation Result { get; set; } = new(0, []);

    public Task<InstalledPlugin> InstallAsync(string packagePath, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<InstalledPlugin?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<InstalledPlugin?>(null);

    public Task<IReadOnlyList<InstalledPlugin>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<InstalledPlugin>>([]);

    public Task<PluginInstallReconciliation> ReconcileAsync(CancellationToken cancellationToken)
    {
        ReconcileCalls++;
        return ReconcileFailure is not null
            ? Task.FromException<PluginInstallReconciliation>(ReconcileFailure)
            : Task.FromResult(Result);
    }
}
