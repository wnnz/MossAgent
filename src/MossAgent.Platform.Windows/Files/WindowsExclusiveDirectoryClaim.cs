using Microsoft.Win32.SafeHandles;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal sealed class WindowsExclusiveDirectoryClaim : IExclusiveDirectoryClaim
{
    private readonly WindowsWriteDirectoryLease _parent;
    private readonly SafeFileHandle _handle;
    private bool _disposed;

    public WindowsExclusiveDirectoryClaim(string path, WindowsWriteDirectoryLease parent)
    {
        _parent = parent;
        Path = path;
        var name = System.IO.Path.GetFileName(path);
        _handle = WindowsRelativeFile.Open(parent.Parent, name, WindowsRelativeFile.DirectoryAccess,
            FileShare.ReadWrite, 2, true, false)
            ?? throw new FileMutationSessionException(WindowsFileText.Get("WriteHandleDenied"), "directory_claim_failed");
        try { Identity = WindowsDirectoryObjectIdentity.Read(_handle, path); }
        catch
        {
            _handle.Dispose();
            throw;
        }
    }

    public string Path { get; }
    public DirectoryObjectIdentity Identity { get; }

    public void Validate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var current = WindowsDirectoryObjectIdentity.Read(_handle, Path);
        if (current != Identity) throw new UnauthorizedAccessException(WindowsFileText.Get("WriteHandleDenied"));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _handle.Dispose(); }
        finally { _parent.Dispose(); }
    }
}
