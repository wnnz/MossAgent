using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Processes;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsJobExtendedLimits
{
    public WindowsJobBasicLimits BasicLimits;
    public WindowsJobIoCounters IoCounters;
    public nuint ProcessMemoryLimit;
    public nuint JobMemoryLimit;
    public nuint PeakProcessMemoryUsed;
    public nuint PeakJobMemoryUsed;
}
