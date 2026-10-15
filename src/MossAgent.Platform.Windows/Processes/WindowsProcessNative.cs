using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace MossAgent.Platform.Windows.Processes;

internal static class WindowsProcessNative
{
    public const uint StartUseStandardHandles = 0x100;
    public const uint CreateSuspended = 0x4;
    public const uint CreateUnicodeEnvironment = 0x400;
    public const uint CreateNoWindow = 0x08000000;
    public const uint ExtendedStartupInfoPresent = 0x00080000;
    public const uint JobLimitKillOnClose = 0x2000;
    public const int JobBasicAccountingInformation = 1;
    public const int JobExtendedLimitInformation = 9;
    public const uint ProcessAttributeHandleList = 0x20002;
    public const uint ProcessAttributeJobList = 0x2000D;

    public static void Require(bool success)
    {
        if (!success) throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeFileHandle CreateJobObject(nint attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass,
        ref WindowsJobExtendedLimits information, uint informationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryInformationJobObject(SafeFileHandle job, int informationClass,
        out WindowsJobAccounting information, uint informationLength, nint returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TerminateJobObject(SafeFileHandle job, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool InitializeProcThreadAttributeList(nint attributes, int count,
        uint flags, ref nuint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UpdateProcThreadAttribute(nint attributes, uint flags, nuint attribute,
        nint value, nuint size, nint previousValue, nint returnSize);

    [DllImport("kernel32.dll")]
    public static extern void DeleteProcThreadAttributeList(nint attributes);

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CreateProcess(string applicationName, StringBuilder commandLine,
        nint processAttributes, nint threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags, nint environment, string workingDirectory,
        ref WindowsStartupInfoEx startupInfo, out WindowsProcessInformation information);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint ResumeThread(SafeFileHandle thread);
}
