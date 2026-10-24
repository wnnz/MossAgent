using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MossAgent.Domain;

namespace MossAgent.Platform.Windows.Files;

internal static class WindowsFileDeletionStatus
{
    // FILE_STANDARD_INFO: AllocationSize(8) EndOfFile(8) NumberOfLinks(4) DeletePending(1) Directory(1) pad.
    // A held-open directory object reports DeletePending once it is unlinked, which is the
    // airtight signal that the removal reached this exact object. Ambiguous native failure
    // must fail closed, so it throws instead of returning a state.
    public static bool IsDeletePending(SafeFileHandle handle)
    {
        var information = new byte[24];
        if (!GetFileInformationByHandleEx(handle, 1, information, (uint)information.Length))
            throw new ProjectRootAuthorizationException();
        return information[20] != 0;
    }

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int informationClass,
        [Out] byte[] information, uint size);
}
