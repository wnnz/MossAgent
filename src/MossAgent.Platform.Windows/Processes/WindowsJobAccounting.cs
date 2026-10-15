using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Processes;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsJobAccounting
{
    public long TotalUserTime;
    public long TotalKernelTime;
    public long ThisPeriodUserTime;
    public long ThisPeriodKernelTime;
    public uint TotalPageFaultCount;
    public uint TotalProcesses;
    public uint ActiveProcesses;
    public uint TotalTerminatedProcesses;
}
