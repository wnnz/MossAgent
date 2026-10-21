using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Files;

internal static class FileChangeSnapshotStorage
{
    public static string DirectoryFor(ToolExecutionContext context)
    {
        var directory = context.ArtifactDirectory ?? throw new FileEditException("snapshot_unavailable", "SnapshotUnavailable");
        RejectLinks(directory);
        return Path.GetFullPath(directory);
    }

    public static void RejectLinks(string path)
    {
        FileSystemInfo current = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        if (current.LinkTarget is not null || current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new UnauthorizedAccessException();
        for (var parent = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path))!); parent is not null; parent = parent.Parent)
            if (parent.LinkTarget is not null || parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException();
    }

    public static async Task<ToolFileSnapshot?> CaptureAsync(
        string sourcePath, string expectedHash, string directory, ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        await using var source = FileSnapshotReader.Open(sourcePath, context);
        return await CaptureAsync(source, expectedHash, directory, cancellationToken);
    }

    public static async Task<ToolFileSnapshot?> CaptureAsync(
        FileStream source, string expectedHash, string directory, CancellationToken cancellationToken)
    {
        if (source.Length > ToolFileSnapshot.MaximumBytes) return null;
        source.Position = 0;
        var version = await FileSnapshotReader.HashAsync(source, cancellationToken);
        if (!string.Equals(version.Sha256, expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new FileEditException("file_changed", "FileChanged");
        source.Position = 0;
        RejectLinks(directory);
        Directory.CreateDirectory(directory);
        RejectLinks(directory);
        var name = $"edit-{Guid.NewGuid():N}.snapshot";
        var destination = Path.Combine(directory, name);
        try
        {
            await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
            {
                await source.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            RejectLinks(destination);
            cancellationToken.ThrowIfCancellationRequested();
            return new(name, source.Length, expectedHash);
        }
        catch { Delete(directory, name); throw; }
    }

    public static void Delete(string directory, string name)
    {
        try { var path = Path.Combine(directory, name); RejectLinks(path); File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
