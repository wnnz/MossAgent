using System.Security.Cryptography;
using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal sealed class WindowsAtomicFileWriteSession : IAtomicFileWriteSession
{
    private readonly WindowsWriteDirectoryLease _directory;
    private readonly string _name;
    private bool _committed;
    private bool _disposed;
    public string TemporaryPath { get; }
    public FileStream Stream { get; }

    public WindowsAtomicFileWriteSession(string path, WindowsWriteDirectoryLease directory, CancellationToken token)
    {
        _directory = directory;
        _name = Path.GetFileName(path);
        ValidateTargetMetadata(token);
        var temporaryName = $".mossagent-{Guid.NewGuid():N}.tmp";
        TemporaryPath = Path.Combine(Path.GetDirectoryName(path)!, temporaryName);
        token.ThrowIfCancellationRequested();
        var handle = WindowsRelativeFile.Open(directory.Parent, temporaryName,
            WindowsRelativeFile.StagingAccess, FileShare.None, 2, false)!;
        try { Stream = new FileStream(handle, FileAccess.ReadWrite, 81920, false); }
        catch
        {
            try { WindowsRelativeFile.DeleteOnClose(handle); }
            finally { handle.Dispose(); }
            throw;
        }
    }

    private void ValidateTargetMetadata(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var existing = WindowsRelativeFile.Open(_directory.Parent, _name,
            WindowsRelativeFile.ReadAccess, FileShare.Read | FileShare.Delete, 1, false, true);
        if (existing is not null) WindowsFileReplacementMetadata.Validate(existing);
    }

    public async Task<bool> TryCommitAsync(string? expectedSha256, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_committed) throw new InvalidOperationException();
        cancellationToken.ThrowIfCancellationRequested();
        await Stream.FlushAsync(cancellationToken);
        using var target = WindowsRelativeFile.Open(_directory.Parent, _name,
            WindowsRelativeFile.ReadAccess | 0x10000, FileShare.Read | FileShare.Delete, 1, false, true);
        if (target is null && expectedSha256 is not null || target is not null && expectedSha256 is null) return false;
        if (target is not null)
        {
            if ((File.GetAttributes(target) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                throw new UnauthorizedAccessException(WindowsFileText.WriteHandleDenied);
            await using var original = new FileStream(target, FileAccess.Read, 81920, false);
            var current = Convert.ToHexString(await SHA256.HashDataAsync(original, cancellationToken));
            if (!current.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase)) return false;
            WindowsFileReplacementMetadata.Preserve(target, Stream.SafeFileHandle);
            cancellationToken.ThrowIfCancellationRequested();
            WindowsRelativeFile.Rename(Stream.SafeFileHandle, _directory.Parent, _name, true);
        }
        else
        {
            cancellationToken.ThrowIfCancellationRequested();
            WindowsRelativeFile.Rename(Stream.SafeFileHandle, _directory.Parent, _name, false);
        }
        _committed = true;
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (!_committed)
            {
                try { WindowsRelativeFile.DeleteOnClose(Stream.SafeFileHandle); }
                catch (IOException) { /* Never delete a path which may now name another object. */ }
            }
        }
        finally
        {
            try { Stream.Dispose(); }
            finally { _directory.Dispose(); }
        }
    }
}
