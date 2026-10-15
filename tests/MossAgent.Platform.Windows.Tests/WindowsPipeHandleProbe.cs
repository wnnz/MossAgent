using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Tests;

internal static class WindowsPipeHandleProbe
{
    public static bool IsInherited(nint handle)
    {
        if (!GetHandleInformation(handle, out var flags)) throw new Win32Exception(Marshal.GetLastPInvokeError());
        return (flags & 1) != 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetHandleInformation(nint handle, out uint flags);
}
