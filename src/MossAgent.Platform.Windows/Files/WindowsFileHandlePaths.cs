using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace MossAgent.Platform.Windows.Files;

internal static class WindowsFileHandlePaths
{
    public static SafeFileHandle OpenDirectory(string path) => CreateFileW(
        path, 0x80, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0x02000000, IntPtr.Zero);

    public static string FinalPath(SafeFileHandle handle, uint volumeFormat)
    {
        if (handle.IsClosed || handle.IsInvalid) throw new UnauthorizedAccessException();
        var buffer = new StringBuilder(512);
        var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, volumeFormat);
        if (length >= buffer.Capacity && length < 32768)
        {
            buffer = new StringBuilder(checked((int)length + 1));
            length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, volumeFormat);
        }
        if (length == 0 || length >= buffer.Capacity) throw new UnauthorizedAccessException();
        return buffer.ToString();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle handle, StringBuilder path, uint pathLength, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName, uint access, FileShare share, IntPtr securityAttributes,
        FileMode creationDisposition, uint flags, IntPtr templateFile);
}
