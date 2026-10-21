using System.Security.Cryptography;
using System.Text;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

public static class FileSnapshotReader
{
    public static string CacheKey(string path) => OperatingSystem.IsWindows()
        ? Path.GetFullPath(path).ToUpperInvariant() : Path.GetFullPath(path);

    public static void ValidateExpectedHash(string? expectedHash)
    {
        if (expectedHash is not null && (expectedHash.Length != 64 || !expectedHash.All(Uri.IsHexDigit)))
            throw new ArgumentException(FileToolText.Get("InvalidHash"));
    }

    public static async Task<FileSnapshot> ReadAsync(
        string path, CancellationToken cancellationToken, ToolExecutionContext? context = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using var stream = Open(path, context);
            return await HashAsync(stream, cancellationToken);
        }
        catch (FileNotFoundException) { return new FileSnapshot(null); }
        catch (DirectoryNotFoundException) { return new FileSnapshot(null); }
    }

    public static async Task<TextFileSnapshot> ReadTextAsync(
        string path, CancellationToken cancellationToken, ToolExecutionContext? context = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = Open(path, context);
        if (stream.Length > 16 * 1024 * 1024)
            throw new FileEditException("file_too_large", "FileTooLarge");
        var version = await HashAsync(stream, cancellationToken);
        stream.Position = 0;
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        return new TextFileSnapshot(content, version, reader.CurrentEncoding);
    }

    public static async Task<FileSnapshot> CaptureForEditAsync(
        string path, ToolExecutionContext context, string? expectedHash, CancellationToken cancellationToken)
    {
        ValidateExpectedHash(expectedHash);
        var version = await ReadAsync(path, cancellationToken, context);
        ValidateForEdit(path, context, version, expectedHash);
        return version;
    }

    public static void ValidateForEdit(
        string path, ToolExecutionContext context, FileSnapshot version, string? expectedHash)
    {
        ValidateExpectedHash(expectedHash);
        var key = CacheKey(path);
        var observed = context.FileSnapshots.Get(context.TaskId, key);
        if (observed is not null && observed != version
            || expectedHash is not null && !expectedHash.Equals(version.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new FileEditException("file_changed", "FileChanged");
        context.FileSnapshots.Record(context.TaskId, key, version);
    }

    public static FileStream Open(string path, ToolExecutionContext? context = null)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read | FileShare.Delete, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        try
        {
            context?.ReadHandleValidator?.Validate(stream, context.AuthorizedRoots);
            return stream;
        }
        catch { stream.Dispose(); throw; }
    }

    public static async Task<FileSnapshot> HashAsync(Stream stream, CancellationToken cancellationToken) =>
        new(Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)));
}
