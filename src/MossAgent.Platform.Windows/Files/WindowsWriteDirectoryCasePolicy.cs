using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MossAgent.Platform.Windows.Resources;

namespace MossAgent.Platform.Windows.Files;

internal static class WindowsWriteDirectoryCasePolicy
{
    public static void Validate(SafeFileHandle directory)
    {
        // This slice deliberately excludes case-sensitive directories, rather than
        // resolving an authorized root to an ambiguously cased sibling.
        if (NtQueryInformationFile(directory, out _, out var flags, 4, 71) < 0 || flags != 0)
            throw new UnauthorizedAccessException(WindowsFileText.WriteHandleDenied);
    }

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtQueryInformationFile(SafeFileHandle handle,
        out WindowsFileIoStatus status, out uint information, uint length, uint informationClass);
}
