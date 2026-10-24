using Microsoft.Win32.SafeHandles;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

// Re-opens the enrolled worktree object by relative name with full delete sharing and
// verifies its identity before git removes anything. The handle pins the object (not the
// name), so a substituted directory can be deleted in its place but ConfirmRemoved then
// detects that the enrolled object survived and fails closed.
internal sealed class WindowsWorktreeRemovalGuard : IWorktreeRemovalGuard
{
    private readonly SafeFileHandle _handle;

    private WindowsWorktreeRemovalGuard(SafeFileHandle handle, string path)
    {
        _handle = handle;
        Path = path;
    }

    public string Path { get; }

    public static WindowsWorktreeRemovalGuard Open(string path, DirectoryObjectIdentity expected,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!System.IO.Path.IsPathFullyQualified(path))
            throw new UnauthorizedAccessException(WindowsFileText.WriteHandleDenied);
        var fullPath = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));
        var name = System.IO.Path.GetFileName(fullPath);
        var parent = System.IO.Path.GetDirectoryName(fullPath);
        if (parent is null) throw new UnauthorizedAccessException(WindowsFileText.WriteHandleDenied);
        WindowsWriteDirectoryLease.ValidateName(name);
        using var parentHandle = WindowsFileHandlePaths.OpenDirectory(parent, WindowsRelativeFile.DirectoryAccess);
        var handle = WindowsRelativeFile.Open(parentHandle, name, WindowsRelativeFile.DirectoryAccess,
            FileShare.ReadWrite | FileShare.Delete, 1u, true, out _, allowMissing: true)
            ?? throw new UnauthorizedAccessException(WindowsFileText.WriteHandleDenied);
        try
        {
            var current = WindowsDirectoryObjectIdentity.Read(handle, fullPath);
            if (!StringComparer.OrdinalIgnoreCase.Equals(current.VolumeId, expected.VolumeId)
                || current.FileId != expected.FileId)
                throw new ProjectRootAuthorizationException();
            return new(handle, fullPath);
        }
        catch { handle.Dispose(); throw; }
    }

    public void ConfirmRemoved()
    {
        if (!WindowsFileDeletionStatus.IsDeletePending(_handle))
            throw new ProjectRootAuthorizationException();
    }

    public void Dispose() => _handle.Dispose();
}
