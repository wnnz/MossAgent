using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>Orchestrates local installs: re-runs the full preview validation, extracts to a
/// unique staging directory, moves the payload into an immutable content-addressed final
/// directory, then records the row. Any failure or cancellation removes the staging tree and
/// leaves no row; a row-write failure removes the moved payload as best effort, leaving
/// reconciliation to report what could not be cleaned.</summary>
public sealed class PluginPackageInstaller(
    IPluginPackagePreviewer previewer,
    IInstalledPluginStore store,
    string pluginRoot,
    TimeProvider? timeProvider = null) : IPluginPackageInstaller
{
    public const string StagingDirectoryName = "_staging";

    private readonly string _pluginRoot = Path.GetFullPath(pluginRoot);
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public async Task<InstalledPlugin> InstallAsync(string packagePath, CancellationToken cancellationToken)
    {
        var preview = await previewer.PreviewAsync(packagePath, cancellationToken);
        var existing = await store.GetByNameVersionAsync(preview.Name, preview.Version, cancellationToken);
        if (existing is not null)
            return SameContent(existing, preview) ? existing : throw Conflict(preview);
        var relative = PluginInstallDirectory.Name(preview.Name, preview.Version, preview.ContentHash);
        var staging = Path.Combine(_pluginRoot, StagingDirectoryName, Guid.NewGuid().ToString("N"));
        var final = Path.Combine(_pluginRoot, relative);
        await GuardedAsync(staging, "the package could not be extracted", async () =>
        {
            Directory.CreateDirectory(staging);
            var extractedHash = await PluginPackageExtractor.ExtractAsync(packagePath, staging, cancellationToken);
            if (!extractedHash.Equals(preview.ContentHash, StringComparison.Ordinal))
                throw Failed("the package changed between preview and installation", null);
            MoveIntoPlace(staging, final);
            return true;
        });
        var plugin = new InstalledPlugin(Guid.NewGuid(), preview.Name, preview.Version, preview.ContentHash,
            relative, InstalledPluginState.Installed, preview.SourcePath, _time.GetUtcNow(),
            preview.Components, preview.DeclaredMcpServers, preview.OverlaySource);
        return await GuardedAsync(final, "the installed plugin row could not be written",
            () => store.InsertAsync(plugin, cancellationToken));
    }

    private async Task<T> GuardedAsync<T>(string cleanup, string detail, Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception error) when (error is not PluginPackageException)
        {
            TryDelete(cleanup);
            if (error is OperationCanceledException) throw;
            throw Failed(detail, error);
        }
        catch (PluginPackageException)
        {
            TryDelete(cleanup);
            throw;
        }
    }

    public Task<InstalledPlugin?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        store.GetAsync(id, cancellationToken);

    public Task<IReadOnlyList<InstalledPlugin>> ListAsync(CancellationToken cancellationToken) =>
        store.ListAsync(cancellationToken);

    /// <summary>Removes leftover staging trees and reports final directories without a row.
    /// Not wired to startup yet; orphaned finals are only listed, never deleted.</summary>
    public async Task<PluginInstallReconciliation> ReconcileAsync(CancellationToken cancellationToken)
    {
        var stagingRoot = Path.Combine(_pluginRoot, StagingDirectoryName);
        var removed = 0;
        if (Directory.Exists(stagingRoot))
            foreach (var leftover in Directory.EnumerateDirectories(stagingRoot))
                if (TryDelete(leftover)) removed++;
        var known = (await store.ListAsync(cancellationToken))
            .Select(row => row.InstallPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orphaned = Directory.Exists(_pluginRoot)
            ? Directory.EnumerateDirectories(_pluginRoot)
                .Select(path => Path.GetFileName(path)!)
                .Where(name => !name.Equals(StagingDirectoryName, StringComparison.OrdinalIgnoreCase)
                    && !known.Contains(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
        return new PluginInstallReconciliation(removed, orphaned);
    }

    public Task<InstalledPlugin?> EnableAsync(Guid id, CancellationToken cancellationToken) =>
        store.SetStateAsync(id, InstalledPluginState.Enabled, cancellationToken);

    public Task<InstalledPlugin?> DisableAsync(Guid id, CancellationToken cancellationToken) =>
        store.SetStateAsync(id, InstalledPluginState.Disabled, cancellationToken);

    public async Task<PluginUninstallOutcome?> UninstallAsync(Guid id, CancellationToken cancellationToken)
    {
        var plugin = await store.DeleteAsync(id, cancellationToken);
        if (plugin is null) return null;
        var payload = PayloadPath(plugin.InstallPath);
        return new PluginUninstallOutcome(plugin, payload is not null && TryDelete(payload));
    }

    private string? PayloadPath(string installPath)
    {
        var path = Path.GetFullPath(Path.Combine(_pluginRoot, installPath));
        var prefix = _pluginRoot.EndsWith(Path.DirectorySeparatorChar)
            ? _pluginRoot : _pluginRoot + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    private void MoveIntoPlace(string staging, string final)
    {
        if (Directory.Exists(final) || File.Exists(final))
            throw new PluginPackageException(PluginPackageErrorCode.InstallConflict,
                $"install directory '{Path.GetFileName(final)}' already exists without a row");
        try
        {
            Directory.Move(staging, final);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw Failed("the staged payload could not be moved into place", error);
        }
    }

    private bool TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static bool SameContent(InstalledPlugin stored, PluginPackagePreview preview) =>
        stored.ContentHash.Equals(preview.ContentHash, StringComparison.Ordinal);

    private static PluginPackageException Conflict(PluginPackagePreview preview) =>
        new(PluginPackageErrorCode.InstallConflict,
            $"a different package already owns '{preview.Name}' {preview.Version}");

    private static PluginPackageException Failed(string detail, Exception? inner) =>
        new(PluginPackageErrorCode.InstallFailed, detail, inner);
}
