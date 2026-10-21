using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal sealed class WindowsPathDeleteSession : IPathDeleteSession
{
    private readonly WindowsWriteDirectoryLease _directory;
    private readonly List<WindowsDeleteEntry> _entries = [];
    private bool _disposed;
    private bool _started;
    public Stream? FileContent { get; private set; }
    public int PlannedCount => _entries.Count;
    public int DeletedCount { get; private set; }

    private WindowsPathDeleteSession(WindowsWriteDirectoryLease directory) => _directory = directory;

    public static WindowsPathDeleteSession Prepare(string path, IReadOnlyList<string> roots,
        bool recursive, bool fileOnly, CancellationToken token)
    {
        var directory = WindowsWriteDirectoryLease.Acquire(path, roots, token, createParents: false);
        var session = new WindowsPathDeleteSession(directory);
        try
        {
            var target = session.Add(directory.Parent, Path.GetFileName(path), token);
            if (target.IsDirectory)
            {
                if (fileOnly) throw new FileDeleteSessionException(WindowsFileText.DeleteFileRequired);
                session.Scan(target, recursive, 0, token);
            }
            else
            {
                if (fileOnly) ValidateUndoMetadata(target);
                session.FileContent = new FileStream(target.Handle, FileAccess.Read, 81920, false);
            }
            token.ThrowIfCancellationRequested();
            return session;
        }
        catch { session.Dispose(); throw; }
    }

    private static void ValidateUndoMetadata(WindowsDeleteEntry target)
    {
        try { WindowsFileReplacementMetadata.Validate(target.Handle); }
        catch (FileWriteSessionException)
        {
            throw new FileDeleteSessionException(WindowsFileText.DeleteMetadataDenied, "file_delete_metadata_unsupported");
        }
    }

    private WindowsDeleteEntry Add(Microsoft.Win32.SafeHandles.SafeFileHandle parent, string name, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_entries.Count >= 4096) throw new FileDeleteSessionException(WindowsFileText.DeleteTreeLimit, "file_delete_limit");
        var entry = new WindowsDeleteEntry(parent, name);
        _entries.Add(entry);
        return entry;
    }

    private void Scan(WindowsDeleteEntry directory, bool recursive, int depth, CancellationToken token)
    {
        foreach (var name in WindowsDirectoryNames.Read(directory.Handle, token))
        {
            if (!recursive) throw new FileDeleteSessionException(WindowsFileText.DeleteRecursiveRequired, "directory_not_empty");
            if (depth >= 64) throw new FileDeleteSessionException(WindowsFileText.DeleteTreeLimit, "file_delete_limit");
            var entry = Add(directory.Handle, name, token);
            if (entry.IsDirectory) Scan(entry, true, depth + 1, token);
        }
    }

    public async Task DeleteAsync(Func<CancellationToken, Task> validateAuthorization, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) throw new InvalidOperationException();
        _started = true;
        for (var index = _entries.Count - 1; index >= 0; index--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await validateAuthorization(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            WindowsRelativeFile.DeleteOnClose(_entries[index].Handle);
            DeletedCount++;
            if (index == 0) FileContent?.Dispose();
            _entries[index].Dispose();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { FileContent?.Dispose(); }
        finally
        {
            for (var index = _entries.Count - 1; index >= 0; index--) _entries[index].Dispose();
            _directory.Dispose();
        }
    }
}
