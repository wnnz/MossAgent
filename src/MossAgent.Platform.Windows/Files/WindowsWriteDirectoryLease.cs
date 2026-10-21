using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using MossAgent.Platform.Windows.Resources;

namespace MossAgent.Platform.Windows.Files;

internal sealed class WindowsWriteDirectoryLease : IDisposable
{
    private readonly List<SafeFileHandle> _handles = [];
    public SafeFileHandle Parent => _handles[^1];

    public static WindowsWriteDirectoryLease Acquire(
        string path, IReadOnlyList<string> roots, CancellationToken token, bool createParents = true)
    {
        token.ThrowIfCancellationRequested();
        if (!Path.IsPathFullyQualified(path)) throw new UnauthorizedAccessException(WindowsFileText.WriteHandleDenied);
        ValidateName(Path.GetFileName(path));
        var fullPath = Path.GetFullPath(path);
        ValidateName(Path.GetFileName(fullPath));
        var root = roots.Where(Path.IsPathFullyQualified).Select(Path.GetFullPath).OrderByDescending(value => value.Length)
            .FirstOrDefault(value => fullPath.StartsWith(
                Path.EndsInDirectorySeparator(value) ? value : value + '\\', StringComparison.OrdinalIgnoreCase));
        if (root is null) throw new UnauthorizedAccessException(WindowsFileText.WriteHandleDenied);
        var drive = Path.GetPathRoot(root)!;
        if (drive.Length != 3 || drive[1] != ':' || GetDriveTypeW(drive) != 3)
            throw new UnauthorizedAccessException(WindowsFileText.WriteHandleDenied);
        var lease = new WindowsWriteDirectoryLease();
        try
        {
            lease.AddDrive(drive);
            var rootParts = Path.TrimEndingDirectorySeparator(root)[drive.Length..]
                .Split('\\', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in rootParts) lease.AddDirectory(part, false, token);
            var relative = Path.GetRelativePath(root, Path.GetDirectoryName(fullPath)!);
            if (relative != ".")
                foreach (var part in relative.Split('\\')) lease.AddDirectory(part, createParents, token);
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    private void AddDrive(string drive)
    {
        var handle = CreateFileW(@"\\?\" + drive, WindowsRelativeFile.DirectoryAccess,
            FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0x02200000, IntPtr.Zero);
        _handles.Add(handle);
        var fileSystem = new StringBuilder(32);
        if (handle.IsInvalid || !GetVolumeInformationByHandleW(handle, null, 0, out _, out _, out _, fileSystem, 32)
            || fileSystem.ToString() != "NTFS"
            || !WindowsFileHandlePaths.FinalPath(handle, 0).Equals(@"\\?\" + drive, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(WindowsFileText.WriteHandleDenied);
    }

    internal void AddDirectory(string name, bool create, CancellationToken token, Action? onCreated = null)
    {
        token.ThrowIfCancellationRequested();
        ValidateName(name);
        var handle = WindowsRelativeFile.Open(Parent, name, WindowsRelativeFile.DirectoryAccess,
            FileShare.ReadWrite, create ? 3u : 1u, true, out var created)!;
        _handles.Add(handle);
        if (created) onCreated?.Invoke();
        var attributes = File.GetAttributes(handle);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != FileAttributes.Directory)
            throw new UnauthorizedAccessException(WindowsFileText.WriteHandleDenied);
    }

    internal static void ValidateName(string name)
    {
        if (string.IsNullOrEmpty(name) || name is "." or ".." || name.EndsWith(' ') || name.EndsWith('.')
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new UnauthorizedAccessException(WindowsFileText.WriteHandleDenied);
    }

    public void Dispose()
    {
        for (var index = _handles.Count - 1; index >= 0; index--) _handles[index].Dispose();
        _handles.Clear();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint GetDriveTypeW(string root);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, FileShare share,
        IntPtr security, FileMode disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformationByHandleW(SafeFileHandle handle,
        StringBuilder? volume, uint volumeLength, out uint serial, out uint componentLength,
        out uint flags, StringBuilder fileSystem, uint fileSystemLength);
}
