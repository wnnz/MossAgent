using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Files;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsFileObjectAttributes
{
    public uint Length;
    public IntPtr RootDirectory;
    public IntPtr ObjectName;
    public uint Attributes;
    public IntPtr SecurityDescriptor;
    public IntPtr SecurityQualityOfService;
}
