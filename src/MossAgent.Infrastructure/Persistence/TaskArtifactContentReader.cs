using System.Security.Cryptography;
using MossAgent.Application.Artifacts;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Persistence;

public sealed class TaskArtifactContentReader(ITaskArtifactPaths paths,
    IFileReadHandleValidator? readHandleValidator = null) : ITaskArtifactContentReader
{
    public Task<byte[]> ReadAsync(Guid taskId, ToolArtifact artifact, CancellationToken cancellationToken) =>
        Task.Run(() => ReadCoreAsync(taskId, artifact, cancellationToken), cancellationToken);

    private async Task<byte[]> ReadCoreAsync(Guid taskId, ToolArtifact artifact, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(paths.GetTaskDirectory(taskId)));
        var path = Path.GetFullPath(artifact.Path);
        ValidatePath(path, root);
        if (artifact.Length is < 0 or > FilePreviewFormat.MaximumBytes) throw new InvalidDataException();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        readHandleValidator?.Validate(stream, [root]);
        cancellationToken.ThrowIfCancellationRequested();
        if (stream.Length != artifact.Length) throw new InvalidDataException();
        var bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        ValidatePath(path, root);
        var mediaType = FilePreviewFormat.Detect(bytes);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(artifact.Sha256, StringComparison.OrdinalIgnoreCase)
            || mediaType != artifact.MediaType && !(mediaType == "text/plain" && artifact.MediaType == "text/x-diff"))
            throw new InvalidDataException();
        return bytes;
    }

    private static void ValidatePath(string path, string root)
    {
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException();
        for (FileSystemInfo? current = new FileInfo(path); current is not null; current =
            current is FileInfo file ? file.Directory : ((DirectoryInfo)current).Parent)
            if (current.LinkTarget is not null) throw new UnauthorizedAccessException();
    }
}
