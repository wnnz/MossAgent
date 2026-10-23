using Microsoft.Win32.SafeHandles;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal sealed class WindowsWorktreeTargetClaim : IWorktreeTargetClaim
{
    private readonly SafeFileHandle _handle;

    private WindowsWorktreeTargetClaim(SafeFileHandle handle, string path, DirectoryObjectIdentity identity)
    {
        _handle = handle;
        Path = path;
        Identity = identity;
    }

    public string Path { get; }
    public DirectoryObjectIdentity Identity { get; }

    public static WindowsWorktreeTargetClaim Create(string path, CancellationToken token)
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
            FileShare.ReadWrite | FileShare.Delete, 2u, true, out var created)
            ?? throw new UnauthorizedAccessException(WindowsFileText.WriteHandleDenied);
        try
        {
            if (!created) throw new UnauthorizedAccessException(WindowsFileText.WriteHandleDenied);
            return new(handle, fullPath, WindowsDirectoryObjectIdentity.Read(handle, fullPath));
        }
        catch { handle.Dispose(); throw; }
    }

    public void Verify()
    {
        var parent = System.IO.Path.GetDirectoryName(Path)
            ?? throw new ProjectRootAuthorizationException();
        using var parentHandle = WindowsFileHandlePaths.OpenDirectory(parent, WindowsRelativeFile.DirectoryAccess);
        using var check = WindowsRelativeFile.Open(parentHandle, System.IO.Path.GetFileName(Path),
            WindowsRelativeFile.DirectoryAccess, FileShare.ReadWrite | FileShare.Delete, 1u, true, out _)
            ?? throw new ProjectRootAuthorizationException();
        var current = WindowsDirectoryObjectIdentity.Read(check, Path);
        if (!StringComparer.OrdinalIgnoreCase.Equals(current.VolumeId, Identity.VolumeId)
            || current.FileId != Identity.FileId)
            throw new ProjectRootAuthorizationException();
    }

    public void Dispose() => _handle.Dispose();
}
