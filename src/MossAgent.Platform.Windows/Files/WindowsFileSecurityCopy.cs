using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal static class WindowsFileSecurityCopy
{
    public static void Copy(SafeFileHandle source, SafeFileHandle destination)
    {
        const uint information = 7; // Owner, group and DACL; never request privileged SACL access.
        GetKernelObjectSecurity(source, information, null, 0, out var size);
        if (size == 0 || size > 1024 * 1024) throw new FileWriteSessionException(WindowsFileText.WriteHandleDenied);
        var descriptor = new byte[size];
        if (!GetKernelObjectSecurity(source, information, descriptor, size, out _)
            || !GetSecurityDescriptorControl(descriptor, out var control, out _))
            throw new FileWriteSessionException(WindowsFileText.WriteHandleDenied);
        var protection = (control & 0x1000) != 0 ? 0x80000000u : 0x20000000u;
        if (!SetKernelObjectSecurity(destination, information | protection, descriptor))
            throw new FileWriteSessionException(WindowsFileText.WriteHandleDenied);
    }

    [DllImport("advapi32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKernelObjectSecurity(SafeFileHandle handle, uint information,
        byte[]? descriptor, uint length, out uint needed);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetKernelObjectSecurity(SafeFileHandle handle, uint information, byte[] descriptor);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSecurityDescriptorControl(byte[] descriptor, out ushort control, out uint revision);
}
