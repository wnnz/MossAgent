using Microsoft.Win32.SafeHandles;
using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal sealed class WindowsDeleteEntry : IDisposable
{
    public SafeFileHandle Handle { get; }
    public bool IsDirectory { get; }

    public WindowsDeleteEntry(SafeFileHandle parent, string name)
    {
        WindowsWriteDirectoryLease.ValidateName(name);
        Handle = WindowsRelativeFile.Open(parent, name,
            WindowsRelativeFile.ReadAccess | 0x10000,
            FileShare.Read, 1, null, allowMissing: true)
            ?? throw new FileDeleteSessionException(WindowsFileText.DeleteNotFound, "path_not_found");
        try
        {
            var attributes = File.GetAttributes(Handle);
            if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.ReadOnly)) != 0)
                throw new FileDeleteSessionException(WindowsFileText.DeleteHandleDenied);
            IsDirectory = (attributes & FileAttributes.Directory) != 0;
            if (IsDirectory) WindowsWriteDirectoryCasePolicy.Validate(Handle);
        }
        catch { Handle.Dispose(); throw; }
    }

    public void Dispose() => Handle.Dispose();
}
