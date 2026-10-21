using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MossAgent.Platform.Windows.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

internal static class AtomicWriteExtendedAttributeFixture
{
    public static void Add(string path)
    {
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        // FILE_FULL_EA_INFORMATION: one "test" name and one-byte value.
        var information = new byte[] { 0, 0, 0, 0, 0, 4, 1, 0, 116, 101, 115, 116, 0, 42 };
        Assert.True(NtSetEaFile(handle, out _, information, (uint)information.Length) >= 0);
    }

    public static uint Size(string path)
    {
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.True(NtQueryInformationFile(handle, out _, out var size, 4, 7) >= 0);
        return size;
    }

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtSetEaFile(SafeFileHandle handle,
        out WindowsFileIoStatus status, byte[] information, uint length);

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtQueryInformationFile(SafeFileHandle handle,
        out WindowsFileIoStatus status, out uint information, uint length, uint informationClass);
}
