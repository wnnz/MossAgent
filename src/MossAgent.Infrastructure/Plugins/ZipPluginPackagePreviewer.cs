using System.IO.Compression;
using System.Security.Cryptography;
using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>Streams a local zip package without extracting it: validates entry hygiene,
/// parses the bounded manifests, inventories components and hashes the original bytes.
/// Nothing is written to disk and no state is persisted.</summary>
public sealed class ZipPluginPackagePreviewer : IPluginPackagePreviewer
{
    public async Task<PluginPackagePreview> PreviewAsync(string packagePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (stream, sourcePath) = Open(packagePath);
        await using (stream)
        {
            var (entries, manifest, servers) = await ReadArchiveAsync(stream, cancellationToken);
            var hash = await HashAsync(stream, cancellationToken);
            var components = PluginComponentInventory.Build(entries, servers);
            return new PluginPackagePreview(manifest.Name, manifest.Version, manifest.Description,
                sourcePath, hash, entries.Count, TotalUncompressedBytes(entries),
                components, manifest.OverlaySource, servers);
        }
    }

    private static (FileStream Stream, string SourcePath) Open(string packagePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(packagePath);
            return (new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan), fullPath);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw Unreadable("the package file could not be opened", error);
        }
    }

    private static async Task<(IReadOnlyList<PluginArchiveEntry>, PluginManifestData, IReadOnlyList<PluginMcpServerDeclaration>)>
        ReadArchiveAsync(FileStream stream, CancellationToken token)
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var entries = PluginArchiveEntryNormalizer.Normalize(archive.Entries);
            var manifest = await PluginManifestParser.ReadAsync(entries, token);
            var servers = await PluginMcpManifestParser.ReadAsync(entries, token);
            return (entries, manifest, servers);
        }
        catch (Exception error) when (error is InvalidDataException or IOException)
        {
            throw Unreadable("the package is not a readable zip archive", error);
        }
    }

    private static async Task<string> HashAsync(FileStream stream, CancellationToken token)
    {
        stream.Position = 0;
        try
        {
            var hash = await SHA256.HashDataAsync(stream, token);
            return Convert.ToHexStringLower(hash);
        }
        catch (IOException error)
        {
            throw Unreadable("the package file could not be hashed", error);
        }
    }

    private static long TotalUncompressedBytes(IReadOnlyList<PluginArchiveEntry> entries) =>
        entries.Where(entry => !entry.IsDirectory).Sum(entry => entry.Length);

    private static PluginPackageException Unreadable(string detail, Exception inner) =>
        new(PluginPackageErrorCode.Unreadable, detail, inner);
}
