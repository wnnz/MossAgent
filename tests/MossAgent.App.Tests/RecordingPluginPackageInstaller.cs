using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.App.Tests;

internal sealed class RecordingPluginPackageInstaller : IPluginPackageInstaller
{
    public int ReconcileCalls { get; private set; }
    public int ListCalls { get; private set; }
    public Exception? ReconcileFailure { get; set; }
    public Exception? ListFailure { get; set; }
    public Exception? InstallFailure { get; set; }
    public Exception? ToggleFailure { get; set; }
    public Exception? UninstallFailure { get; set; }
    public PluginInstallReconciliation Result { get; set; } = new(0, []);
    public IReadOnlyList<InstalledPlugin> Installed { get; set; } = [];
    public List<string> InstallPaths { get; } = [];
    public List<Guid> EnabledIds { get; } = [];
    public List<Guid> DisabledIds { get; } = [];
    public List<Guid> UninstalledIds { get; } = [];
    public Func<string, InstalledPlugin>? InstallHandler { get; set; }
    public Func<Guid, bool, InstalledPlugin?>? ToggleHandler { get; set; }
    public Func<Guid, PluginUninstallOutcome?>? UninstallHandler { get; set; }

    public Task<InstalledPlugin> InstallAsync(string packagePath, CancellationToken cancellationToken)
    {
        InstallPaths.Add(packagePath);
        if (InstallFailure is not null) return Task.FromException<InstalledPlugin>(InstallFailure);
        return Task.FromResult(InstallHandler?.Invoke(packagePath) ?? throw new NotSupportedException());
    }

    public Task<InstalledPlugin?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<InstalledPlugin?>(null);

    public Task<IReadOnlyList<InstalledPlugin>> ListAsync(CancellationToken cancellationToken)
    {
        ListCalls++;
        return ListFailure is not null
            ? Task.FromException<IReadOnlyList<InstalledPlugin>>(ListFailure)
            : Task.FromResult(Installed);
    }

    public Task<PluginInstallReconciliation> ReconcileAsync(CancellationToken cancellationToken)
    {
        ReconcileCalls++;
        return ReconcileFailure is not null
            ? Task.FromException<PluginInstallReconciliation>(ReconcileFailure)
            : Task.FromResult(Result);
    }

    public Task<InstalledPlugin?> EnableAsync(Guid id, CancellationToken cancellationToken)
    {
        EnabledIds.Add(id);
        if (ToggleFailure is not null) return Task.FromException<InstalledPlugin?>(ToggleFailure);
        return Task.FromResult(ToggleHandler?.Invoke(id, true));
    }

    public Task<InstalledPlugin?> DisableAsync(Guid id, CancellationToken cancellationToken)
    {
        DisabledIds.Add(id);
        if (ToggleFailure is not null) return Task.FromException<InstalledPlugin?>(ToggleFailure);
        return Task.FromResult(ToggleHandler?.Invoke(id, false));
    }

    public Task<PluginUninstallOutcome?> UninstallAsync(Guid id, CancellationToken cancellationToken)
    {
        UninstalledIds.Add(id);
        if (UninstallFailure is not null) return Task.FromException<PluginUninstallOutcome?>(UninstallFailure);
        return Task.FromResult(UninstallHandler?.Invoke(id));
    }
}
