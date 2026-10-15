using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Processes;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsJobIoCounters
{
    public ulong ReadOperationCount;
    public ulong WriteOperationCount;
    public ulong OtherOperationCount;
    public ulong ReadTransferCount;
    public ulong WriteTransferCount;
    public ulong OtherTransferCount;
}
