using Microsoft.Win32.SafeHandles;
using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal sealed class WindowsPathQuarantineSession : IPathQuarantineSession
{
    private readonly WindowsWriteDirectoryLease _sourceParent;
    private readonly SafeFileHandle _source;
    private readonly string _destination;
    private readonly IReadOnlyList<string> _roots;
    private bool _deletionStarted;
    private bool _disposed;

    public WindowsPathQuarantineSession(string source, string destination,
        IReadOnlyList<string> roots, WindowsWriteDirectoryLease sourceParent,
        CancellationToken cancellationToken)
        : this(destination, roots, sourceParent, OpenSource(sourceParent, Path.GetFileName(source)),
            cancellationToken)
    {
    }

    private WindowsPathQuarantineSession(string destination, IReadOnlyList<string> roots,
        WindowsWriteDirectoryLease sourceParent, SafeFileHandle source,
        CancellationToken cancellationToken)
    {
        _sourceParent = sourceParent;
        _destination = destination;
        _roots = roots;
        _source = source;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attributes = File.GetAttributes(_source);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint))
                != FileAttributes.Directory)
                throw new UnauthorizedAccessException(WindowsFileText.Get("MoveDenied"));
            WindowsWriteDirectoryCasePolicy.Validate(_source);
        }
        catch
        {
            _source.Dispose();
            throw;
        }
    }

    internal static WindowsPathQuarantineSession FromPinnedRoot(
        string destination, IReadOnlyList<string> roots,
        WindowsWriteDirectoryLease sourceRoot, CancellationToken cancellationToken) =>
        new(destination, roots, sourceRoot, sourceRoot.Parent, cancellationToken);

    private static SafeFileHandle OpenSource(
        WindowsWriteDirectoryLease sourceParent, string name) =>
        WindowsRelativeFile.Open(sourceParent.Parent, name,
            WindowsRelativeFile.ReadAccess | 0x10000, FileShare.Read, 1, null, true)
        ?? throw new FileMutationSessionException(WindowsFileText.Get("MoveNotFound"),
            "path_not_found");

    public bool Moved { get; private set; }

    public async Task MoveAsync(Func<CancellationToken, Task> validateAuthorization,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Moved) throw new InvalidOperationException();
        await validateAuthorization(cancellationToken).ConfigureAwait(false);
        using var parent = WindowsWriteDirectoryLease.Acquire(
            _destination, _roots, cancellationToken, createParents: false);
        using var target = WindowsRelativeFile.Open(parent.Parent,
            Path.GetFileName(_destination), WindowsRelativeFile.ReadAccess | 0x10000,
            FileShare.Read | FileShare.Delete, 1, null, true);
        if (target is not null)
            throw new FileMutationSessionException(WindowsFileText.Get("MoveDenied"),
                "quarantine_target_exists");
        await validateAuthorization(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        WindowsRelativeFile.Rename(_source, parent.Parent, Path.GetFileName(_destination), false);
        Moved = true;
    }

    public async Task DeleteMovedAsync(Func<CancellationToken, Task> validateAuthorization,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Moved || _deletionStarted) throw new InvalidOperationException();
        _deletionStarted = true;
        var entries = new List<WindowsDeleteEntry>();
        try
        {
            Scan(_source, entries, 0, cancellationToken);
            for (var index = entries.Count - 1; index >= 0; index--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await validateAuthorization(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                WindowsRelativeFile.DeleteOnClose(entries[index].Handle);
                entries[index].Dispose();
            }
            await validateAuthorization(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            WindowsRelativeFile.DeleteOnClose(_source);
            _source.Dispose();
        }
        finally
        {
            foreach (var entry in entries) entry.Dispose();
        }
    }

    private static void Scan(SafeFileHandle directory, List<WindowsDeleteEntry> entries,
        int depth, CancellationToken cancellationToken)
    {
        if (depth >= 64)
            throw new FileDeleteSessionException(WindowsFileText.DeleteTreeLimit,
                "file_delete_limit");
        foreach (var name in WindowsDirectoryNames.Read(directory, cancellationToken))
        {
            if (entries.Count >= 4096)
                throw new FileDeleteSessionException(WindowsFileText.DeleteTreeLimit,
                    "file_delete_limit");
            var entry = new WindowsDeleteEntry(directory, name);
            entries.Add(entry);
            if (entry.IsDirectory) Scan(entry.Handle, entries, depth + 1, cancellationToken);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _source.Dispose(); }
        finally { _sourceParent.Dispose(); }
    }
}
