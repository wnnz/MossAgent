using System.Security.Cryptography;
using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>Orchestrates local installs: re-runs the full preview validation, extracts to a
/// unique staging directory, moves the payload into an immutable content-addressed final
/// directory, then records the row together with the per-component payload hashes the
/// extraction produced. A package whose name is already installed under a different version is
/// an upgrade: the store atomically replaces every same-name row and the replaced payload
/// directories are removed best-effort after commit, so two versions of one name never
/// coexist. Any failure or cancellation removes the staging tree and leaves no
/// row; a row-write failure removes the moved payload as best effort, leaving reconciliation
/// to report what could not be cleaned. Enablement refuses drifted rows; reconciliation also
/// backfills the hash baseline for rows installed before baselines existed, hashing only
/// component files still present under the install directory.</summary>
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
        var replaced = await store.ListByNameAsync(preview.Name, cancellationToken);
        var relative = PluginInstallDirectory.Name(preview.Name, preview.Version, preview.ContentHash);
        var staging = Path.Combine(_pluginRoot, StagingDirectoryName, Guid.NewGuid().ToString("N"));
        var final = Path.Combine(_pluginRoot, relative);
        var payloadHashes = await GuardedAsync(staging, "the package could not be extracted", async () =>
        {
            Directory.CreateDirectory(staging);
            var extraction = await PluginPackageExtractor.ExtractAsync(packagePath, staging, cancellationToken);
            if (!extraction.ContentHash.Equals(preview.ContentHash, StringComparison.Ordinal))
                throw Failed("the package changed between preview and installation", null);
            var hashes = ComponentHashes(preview.Components, extraction);
            MoveIntoPlace(staging, final);
            return hashes;
        });
        var plugin = new InstalledPlugin(Guid.NewGuid(), preview.Name, preview.Version, preview.ContentHash,
            relative, InstalledPluginState.Installed, preview.SourcePath, _time.GetUtcNow(),
            preview.Components, preview.DeclaredMcpServers, preview.OverlaySource);
        var installed = await GuardedAsync(final, "the installed plugin row could not be written",
            () => replaced.Count == 0
                ? store.InsertAsync(plugin, payloadHashes, cancellationToken)
                : store.ReplaceByNameAsync(plugin, payloadHashes, cancellationToken));
        foreach (var row in replaced)
        {
            if (row.InstallPath.Equals(installed.InstallPath, StringComparison.OrdinalIgnoreCase))
                continue;
            var payload = PayloadPath(row.InstallPath);
            if (payload is not null) TryDelete(payload);
        }
        return installed;
    }

    /// <summary>The install-time baseline: exactly the file-backed component paths, each
    /// pinned to the hash extraction recorded for the same archive entry. A component whose
    /// path is an archive directory entry carries no payload of its own — its children are
    /// hashed individually — so it gets no row. A component path that is neither an extracted
    /// file nor an extracted directory means the manifest and archive disagree: the install
    /// fails closed.</summary>
    private static IReadOnlyDictionary<string, string> ComponentHashes(
        IReadOnlyList<PluginComponentDescriptor> components, PluginExtractionResult extraction)
    {
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in components)
        {
            if (extraction.FileHashes.TryGetValue(component.RelativePath, out var hash))
            {
                hashes[component.RelativePath] = hash;
                continue;
            }
            if (extraction.Directories.Contains(component.RelativePath)) continue;
            throw Failed($"component '{component.RelativePath}' has no extracted payload file", null);
        }
        return hashes;
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

    /// <summary>Removes leftover staging trees, reports final directories without a row and
    /// backfills missing payload-hash rows for installed plugins. Orphaned finals are only
    /// listed, never deleted. Backfill establishes the baseline honestly for rows installed
    /// before baselines existed: only component files still present under the plugin's own
    /// install directory are hashed; a file missing on disk gets no row, so its component
    /// fails closed at dispatch.</summary>
    public async Task<PluginInstallReconciliation> ReconcileAsync(CancellationToken cancellationToken)
    {
        var stagingRoot = Path.Combine(_pluginRoot, StagingDirectoryName);
        var removed = 0;
        if (Directory.Exists(stagingRoot))
            foreach (var leftover in Directory.EnumerateDirectories(stagingRoot))
                if (TryDelete(leftover)) removed++;
        var plugins = await store.ListAsync(cancellationToken);
        var known = plugins.Select(row => row.InstallPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orphaned = Directory.Exists(_pluginRoot)
            ? Directory.EnumerateDirectories(_pluginRoot)
                .Select(path => Path.GetFileName(path)!)
                .Where(name => !name.Equals(StagingDirectoryName, StringComparison.OrdinalIgnoreCase)
                    && !known.Contains(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
        await BackfillPayloadHashesAsync(plugins, cancellationToken);
        return new PluginInstallReconciliation(removed, orphaned);
    }

    private async Task BackfillPayloadHashesAsync(IReadOnlyList<InstalledPlugin> plugins,
        CancellationToken cancellationToken)
    {
        foreach (var plugin in plugins)
        {
            var stored = await store.ListPayloadHashesAsync(plugin.Id, cancellationToken);
            var baseline = new HashSet<string>(stored.Keys, StringComparer.OrdinalIgnoreCase);
            var missing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var component in plugin.Components)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!baseline.Add(component.RelativePath)) continue;
                var path = PluginInstallPaths.Resolve(_pluginRoot, plugin.InstallPath, component.RelativePath);
                if (path is null || !File.Exists(path)) continue;
                missing[component.RelativePath] = await HashFileAsync(path, cancellationToken);
            }
            if (missing.Count > 0)
                await store.SavePayloadHashesAsync(plugin.Id, missing, cancellationToken);
        }
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    /// <summary>Marks a row enabled, unless it is drifted: a payload that no longer matches
    /// its install-time baseline is never re-enabled — the only recovery is uninstall and
    /// reinstall. Idempotent and null when the id is gone.</summary>
    public async Task<InstalledPlugin?> EnableAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await store.GetAsync(id, cancellationToken);
        if (row is not null && row.State == InstalledPluginState.Drifted)
            throw new PluginPackageException(PluginPackageErrorCode.PayloadDrifted,
                $"plugin '{row.Name}' {row.Version} payload drifted from its install-time hashes");
        return await store.SetStateAsync(id, InstalledPluginState.Enabled, cancellationToken);
    }

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
