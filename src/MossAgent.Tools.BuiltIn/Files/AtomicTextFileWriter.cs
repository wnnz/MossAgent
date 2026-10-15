using System.Text;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class AtomicTextFileWriter(AuthorizedPathResolver paths)
{
    private readonly SemaphoreSlim[] _gates = Enumerable.Range(0, 128)
        .Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public Task<FileSnapshot> WriteTextAsync(
        string path, string content, Encoding encoding, FileSnapshot original,
        ToolExecutionContext context, CancellationToken cancellationToken) =>
        WriteAsync(path, original, context, (stream, token) => WriteTextContentAsync(stream, content, encoding, token), cancellationToken);

    public async Task<FileWriteOutcome> WriteTextWithHistoryAsync(
        string path, string content, Encoding encoding, FileSnapshot original,
        ToolExecutionContext context, CancellationToken cancellationToken)
    {
        path = paths.Resolve(path, context);
        using var history = new FileChangeSnapshotWriter(context);
        var updated = await WriteCoreAsync(path, original, context,
            (stream, token) => WriteTextContentAsync(stream, content, encoding, token), history, cancellationToken);
        ToolFileChange? change = null;
        if (original.Sha256 != updated.Sha256)
        {
            history.Commit();
            change = new(path, original.Sha256, updated.Sha256!, history.Before, history.After);
        }
        return new(updated, change);
    }

    public Task<FileSnapshot> WriteAsync(
        string path, FileSnapshot original, ToolExecutionContext context,
        Func<Stream, CancellationToken, Task> writeContent, CancellationToken cancellationToken) =>
        WriteCoreAsync(path, original, context, writeContent, null, cancellationToken);

    public async Task<FileSnapshot> DeleteAsync(
        string path, FileSnapshot original, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        path = paths.Resolve(path, context);
        paths.RejectAuthorizedRootMutation(path, context);
        using var lease = await AcquireAsync(path, cancellationToken);
        await VerifyAsync(path, original, context, cancellationToken);
        if (!original.Exists) throw new FileEditException("file_changed", "FileChanged");
        paths.Resolve(path, context);
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(path);
        var deleted = new FileSnapshot(null);
        context.FileSnapshots.Record(context.TaskId, FileSnapshotReader.CacheKey(path), deleted);
        return deleted;
    }

    private async Task<FileSnapshot> WriteCoreAsync(
        string path, FileSnapshot original, ToolExecutionContext context,
        Func<Stream, CancellationToken, Task> writeContent, FileChangeSnapshotWriter? history, CancellationToken cancellationToken)
    {
        path = paths.Resolve(path, context);
        using var lease = await AcquireAsync(path, cancellationToken);
        await VerifyAsync(path, original, context, cancellationToken);
        var cleanupContext = context with { AuthorizedRoots = context.AuthorizedRoots.ToArray() };
        var directory = Path.GetDirectoryName(path) ?? throw new IOException("Missing parent directory.");
        Directory.CreateDirectory(paths.Resolve(directory, context));
        var temporary = paths.Resolve(Path.Combine(directory, $".mossagent-{Guid.NewGuid():N}.tmp"), context);
        try
        {
            var updated = await StageAsync(temporary, writeContent, cancellationToken);
            if (history is not null && original.Sha256 != updated.Sha256)
            {
                await history.CaptureBeforeAsync(paths.Resolve(path, context), original, cancellationToken);
                await history.CaptureAfterAsync(paths.Resolve(temporary, context), updated, cancellationToken);
            }
            await VerifyAsync(path, original, context, cancellationToken);
            paths.Resolve(path, context);
            cancellationToken.ThrowIfCancellationRequested();
            if (original.Exists) File.Replace(temporary, path, null);
            else File.Move(temporary, path, false);
            context.FileSnapshots.Record(context.TaskId, FileSnapshotReader.CacheKey(path), updated);
            return updated;
        }
        finally
        {
            try { File.Delete(paths.Resolve(temporary, cleanupContext)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task WriteTextContentAsync(Stream stream, string content, Encoding encoding, CancellationToken token)
    {
        await using var writer = new StreamWriter(stream, encoding, leaveOpen: true);
        await writer.WriteAsync(content.AsMemory(), token);
        await writer.FlushAsync(token);
    }

    private async Task<FileMutationLease> AcquireAsync(string path, CancellationToken cancellationToken)
    {
        var key = FileSnapshotReader.CacheKey(path);
        var index = (uint)StringComparer.Ordinal.GetHashCode(key) % (uint)_gates.Length;
        var gate = _gates[index];
        await gate.WaitAsync(cancellationToken);
        return new FileMutationLease(gate);
    }

    private async Task VerifyAsync(
        string path, FileSnapshot original, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        paths.Resolve(path, context);
        var current = await FileSnapshotReader.ReadAsync(path, cancellationToken);
        if (current != original) throw new FileEditException("file_changed", "FileChanged");
        FileSnapshotReader.ValidateForEdit(path, context, current, null);
    }

    private static async Task<FileSnapshot> StageAsync(
        string temporary, Func<Stream, CancellationToken, Task> writeContent, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite,
            FileShare.None, 81920, FileOptions.Asynchronous);
        await writeContent(stream, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Position = 0;
        return await FileSnapshotReader.HashAsync(stream, cancellationToken);
    }
}
