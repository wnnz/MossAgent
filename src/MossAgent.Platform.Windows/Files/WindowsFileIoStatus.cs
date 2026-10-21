using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Files;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsFileIoStatus
{
    public IntPtr Status;
    public UIntPtr Information;
}
