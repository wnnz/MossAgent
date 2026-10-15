using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Processes;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsProcessInformation
{
    public nint Process;
    public nint Thread;
    public uint ProcessId;
    public uint ThreadId;
}
