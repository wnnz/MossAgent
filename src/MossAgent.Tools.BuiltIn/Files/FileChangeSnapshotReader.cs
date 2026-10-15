using System.Security.Cryptography;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class FileChangeSnapshotReader
{
    public async Task<(byte[] Before, byte[] After)> ReadChangeAsync(
        ToolFileChange change, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!change.IsValid) throw new FileEditException("snapshot_invalid", "SnapshotInvalid");
        if (change.BeforeSha256 is not null && change.BeforeSnapshot is null
            || change.AfterSha256 is not null && change.AfterSnapshot is null)
            throw new FileEditException("snapshot_unavailable", "SnapshotUnavailable");
        var before = change.BeforeSnapshot is null ? [] : await ReadAsync(change.BeforeSnapshot, context, cancellationToken);
        var after = change.AfterSnapshot is null ? [] : await ReadAsync(change.AfterSnapshot, context, cancellationToken);
        return (before, after);
    }

    public async Task<byte[]> ReadAsync(ToolFileSnapshot snapshot, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!snapshot.IsValid) throw new FileEditException("snapshot_invalid", "SnapshotInvalid");
        var path = Path.Combine(FileChangeSnapshotStorage.DirectoryFor(context), snapshot.FileName);
        FileChangeSnapshotStorage.RejectLinks(path);
        try
        {
            await using var stream = FileSnapshotReader.Open(path);
            if (stream.Length != snapshot.Length) throw new FileEditException("snapshot_invalid", "SnapshotInvalid");
            var bytes = new byte[checked((int)snapshot.Length)];
            await stream.ReadExactlyAsync(bytes, cancellationToken);
            FileChangeSnapshotStorage.RejectLinks(path);
            cancellationToken.ThrowIfCancellationRequested();
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            if (!string.Equals(hash, snapshot.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new FileEditException("snapshot_invalid", "SnapshotInvalid");
            return bytes;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new FileEditException("snapshot_invalid", "SnapshotInvalid");
        }
    }
}
