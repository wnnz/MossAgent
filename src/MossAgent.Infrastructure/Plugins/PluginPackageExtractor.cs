using System.IO.Compression;
using System.Security.Cryptography;
using MossAgent.Application.Plugins;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>Extracts a package into a fresh staging directory with the full entry hygiene
/// re-run locally — the archive bytes are the only input, never a prior preview. Every file is
/// written under a path proven inside the staging root, copied in bounded chunks with a hard
/// byte cap regardless of the declared length, hashed incrementally while it is written, and
/// verified byte-for-byte afterwards. The result carries the whole-archive hash plus each
/// file's payload hash so the installer can pin a per-component baseline.</summary>
internal static class PluginPackageExtractor
{
    private const int CopyBufferBytes = 64 * 1024;

    public static async Task<PluginExtractionResult> ExtractAsync(string packagePath, string stagingRoot,
        CancellationToken token)
    {
        var root = Path.GetFullPath(stagingRoot);
        var stream = Open(packagePath);
        await using (stream)
        {
            using var archive = OpenArchive(stream);
            var entries = PluginArchiveEntryNormalizer.Normalize(archive.Entries);
            var fileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                var hash = await ExtractEntryAsync(entry, root, token);
                if (entry.IsDirectory) directories.Add(entry.NormalizedName);
                else if (hash is not null) fileHashes[entry.NormalizedName] = hash;
            }
            stream.Position = 0;
            var contentHash = await SHA256.HashDataAsync(stream, token);
            return new PluginExtractionResult(Convert.ToHexStringLower(contentHash), fileHashes,
                directories);
        }
    }

    private static FileStream Open(string packagePath)
    {
        try
        {
            return new FileStream(Path.GetFullPath(packagePath), FileMode.Open, FileAccess.Read,
                FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw Unreadable("the package file could not be opened", error);
        }
    }

    private static ZipArchive OpenArchive(FileStream stream)
    {
        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (Exception error) when (error is InvalidDataException or IOException)
        {
            throw Unreadable("the package is not a readable zip archive", error);
        }
    }

    private static async Task<string?> ExtractEntryAsync(PluginArchiveEntry entry, string root,
        CancellationToken token)
    {
        var fullPath = Path.GetFullPath(Path.Combine(root, entry.NormalizedName));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new PluginPackageException(PluginPackageErrorCode.EntryInvalid,
                $"entry '{entry.NormalizedName}' escapes the staging root");
        try
        {
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(fullPath);
                return null;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            return await CopyFileAsync(entry, fullPath, token);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw Failed($"entry '{entry.NormalizedName}' could not be written", error);
        }
    }

    private static async Task<string> CopyFileAsync(PluginArchiveEntry entry, string fullPath,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Stream source;
        try
        {
            source = entry.Entry.Open();
        }
        catch (Exception error) when (error is InvalidDataException or IOException)
        {
            throw Unreadable($"entry '{entry.NormalizedName}' could not be opened", error);
        }
        await using (source)
        {
            await using var destination = new FileStream(fullPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, CopyBufferBytes, FileOptions.Asynchronous);
            return await BoundedCopyAsync(entry, source, destination, token);
        }
    }

    private static async Task<string> BoundedCopyAsync(PluginArchiveEntry entry, Stream source,
        FileStream destination, CancellationToken token)
    {
        var buffer = new byte[CopyBufferBytes];
        var written = 0L;
        var crc = ArchiveCrc32.Start();
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        while (true)
        {
            int read;
            try
            {
                read = await source.ReadAsync(buffer.AsMemory(), token);
            }
            catch (Exception error) when (error is InvalidDataException or IOException)
            {
                throw Unreadable($"entry '{entry.NormalizedName}' could not be decompressed", error);
            }
            if (read == 0) break;
            crc = ArchiveCrc32.Append(crc, buffer.AsSpan(0, read));
            hasher.AppendData(buffer, 0, read);
            await destination.WriteAsync(buffer.AsMemory(0, read), token);
            written += read;
            if (written > PluginArchiveLimits.MaximumFileBytes)
                throw new PluginPackageException(PluginPackageErrorCode.TooLarge,
                    $"entry '{entry.NormalizedName}' exceeds the single-file limit");
        }
        if (ArchiveCrc32.Finish(crc) != (uint)entry.Entry.Crc32 || written != entry.Length)
            throw Failed($"entry '{entry.NormalizedName}' failed its integrity check", null);
        return Convert.ToHexStringLower(hasher.GetHashAndReset());
    }

    private static PluginPackageException Unreadable(string detail, Exception inner) =>
        new(PluginPackageErrorCode.Unreadable, detail, inner);

    private static PluginPackageException Failed(string detail, Exception? inner) =>
        new(PluginPackageErrorCode.InstallFailed, detail, inner);
}
