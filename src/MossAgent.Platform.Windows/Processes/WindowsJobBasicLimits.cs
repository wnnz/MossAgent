using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Processes;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsJobBasicLimits
{
    public long PerProcessUserTimeLimit;
    public long PerJobUserTimeLimit;
    public uint LimitFlags;
    public nuint MinimumWorkingSetSize;
    public nuint MaximumWorkingSetSize;
    public uint ActiveProcessLimit;
    public nuint Affinity;
    public uint PriorityClass;
    public uint SchedulingClass;
}
