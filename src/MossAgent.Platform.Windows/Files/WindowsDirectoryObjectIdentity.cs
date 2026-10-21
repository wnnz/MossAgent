using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MossAgent.Domain;

namespace MossAgent.Platform.Windows.Files;

internal static class WindowsDirectoryObjectIdentity
{
    public static DirectoryObjectIdentity Read(SafeFileHandle handle, string path)
    {
        var information = new byte[24];
        if (!GetFileInformationByHandleEx(handle, 18, information, (uint)information.Length))
            throw new ProjectRootAuthorizationException();
        var physical = WindowsFileHandlePaths.FinalPath(handle, 0);
        if (!physical.Equals(@"\\?\" + path, StringComparison.OrdinalIgnoreCase))
            throw new ProjectRootAuthorizationException();
        var volumePath = WindowsFileHandlePaths.FinalPath(handle, 1);
        var end = volumePath.IndexOf('}');
        if (!volumePath.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase) || end < 0)
            throw new ProjectRootAuthorizationException();
        var serial = BinaryPrimitives.ReadUInt64LittleEndian(information);
        return new(path, volumePath[..(end + 1)].ToUpperInvariant() + ":" + serial.ToString("X16"),
            Convert.ToHexString(information.AsSpan(8, 16)));
    }

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int informationClass,
        [Out] byte[] information, uint size);
}
