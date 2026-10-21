using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Files;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsFileUnicodeString
{
    public ushort Length;
    public ushort MaximumLength;
    public IntPtr Buffer;
}
