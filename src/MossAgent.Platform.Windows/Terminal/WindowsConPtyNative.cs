using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MossAgent.Platform.Windows.Terminal;

/// <summary>P/Invoke declarations for the kernel ConPTY surface.</summary>
internal static class WindowsConPtyNative
{
    public const uint ProcessAttributePseudoConsole = 0x20016;

    [DllImport("kernel32.dll")]
    public static extern int CreatePseudoConsole(WindowsConPtyCoord size, nint input,
        nint output, uint flags, out nint pseudoconsole);

    [DllImport("kernel32.dll")]
    public static extern int ResizePseudoConsole(nint pseudoconsole, WindowsConPtyCoord size);

    [DllImport("kernel32.dll")]
    public static extern void ClosePseudoConsole(nint pseudoconsole);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CreatePipe(out SafeFileHandle readPipe, out SafeFileHandle writePipe,
        nint attributes, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);
}
