using Microsoft.Win32.SafeHandles;
using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal sealed class WindowsFileTreeReadSession : IFileTreeReadSession
{
    private readonly WindowsWriteDirectoryLease _lease;
    private readonly List<SafeFileHandle> _directories = [];
    private readonly List<FileTreeReadEntry> _entries = [];
    public IReadOnlyList<FileTreeReadEntry> Entries => _entries;

    private WindowsFileTreeReadSession(WindowsWriteDirectoryLease lease) => _lease = lease;

    public static WindowsFileTreeReadSession Prepare(string path, IReadOnlyList<string> roots, CancellationToken token)
    {
        if (!Path.IsPathFullyQualified(path)) throw new UnauthorizedAccessException();
        path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var isRoot = roots.Any(root => path.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), StringComparison.OrdinalIgnoreCase));
        var lease = WindowsWriteDirectoryLease.Acquire(isRoot ? Path.Combine(path, ".mossagent-read-anchor") : path, roots, token, false);
        var session = new WindowsFileTreeReadSession(lease);
        try
        {
            var root = isRoot ? lease.Parent : session.Add(lease.Parent, Path.GetFileName(path), "", token);
            if (isRoot) { WindowsWriteDirectoryCasePolicy.Validate(root!); session._entries.Add(new("", null)); }
            if (root is not null) session.Scan(root, "", 0, token);
            token.ThrowIfCancellationRequested();
            return session;
        }
        catch { session.Dispose(); throw; }
    }

    private SafeFileHandle? Add(SafeFileHandle parent, string name, string relative, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_entries.Count >= 4096) throw new FileMutationSessionException(WindowsFileText.Get("CopyTreeLimit"), "file_tree_limit");
        WindowsWriteDirectoryLease.ValidateName(name);
        var handle = WindowsRelativeFile.Open(parent, name, WindowsRelativeFile.ReadAccess, FileShare.Read, 1, null)!;
        try
        {
            var attributes = File.GetAttributes(handle);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new UnauthorizedAccessException();
            if ((attributes & FileAttributes.Directory) != 0)
            {
                WindowsWriteDirectoryCasePolicy.Validate(handle);
                _directories.Add(handle);
                _entries.Add(new(relative, null));
                return handle;
            }
            WindowsFileReplacementMetadata.ValidateStreams(handle);
            _entries.Add(new(relative, new FileStream(handle, FileAccess.Read, 1, false)));
            return null;
        }
        catch { handle.Dispose(); throw; }
    }

    private void Scan(SafeFileHandle directory, string relative, int depth, CancellationToken token)
    {
        foreach (var name in WindowsDirectoryNames.Read(directory, token))
        {
            if (depth >= 64) throw new FileMutationSessionException(WindowsFileText.Get("CopyTreeLimit"), "file_tree_limit");
            var childPath = Path.Combine(relative, name);
            var child = Add(directory, name, childPath, token);
            if (child is not null) Scan(child, childPath, depth + 1, token);
        }
    }

    public void Dispose()
    {
        foreach (var entry in _entries) entry.Stream?.Dispose();
        for (var index = _directories.Count - 1; index >= 0; index--) _directories[index].Dispose();
        _lease.Dispose();
    }
}
