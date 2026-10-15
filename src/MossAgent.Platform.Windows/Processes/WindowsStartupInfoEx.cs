using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Processes;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsStartupInfoEx
{
    public WindowsStartupInfo StartupInfo;
    public nint AttributeList;
}
