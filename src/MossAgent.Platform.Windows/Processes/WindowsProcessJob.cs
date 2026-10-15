using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MossAgent.Platform.Windows.Processes;

internal sealed class WindowsProcessJob : IDisposable
{
    public SafeFileHandle Handle { get; } = WindowsProcessNative.CreateJobObject(0, null);

    public WindowsProcessJob()
    {
        try
        {
            WindowsProcessNative.Require(!Handle.IsInvalid);
            var limits = new WindowsJobExtendedLimits
            {
                BasicLimits = new WindowsJobBasicLimits { LimitFlags = WindowsProcessNative.JobLimitKillOnClose }
            };
            WindowsProcessNative.Require(WindowsProcessNative.SetInformationJobObject(Handle,
                WindowsProcessNative.JobExtendedLimitInformation,
                ref limits, (uint)Marshal.SizeOf<WindowsJobExtendedLimits>()));
        }
        catch { Handle.Dispose(); throw; }
    }

    public async Task TerminateAsync()
    {
        if (ActiveProcesses() == 0) return;
        WindowsProcessNative.Require(WindowsProcessNative.TerminateJobObject(Handle, 1));
        var wait = Stopwatch.StartNew();
        while (ActiveProcesses() > 0)
        {
            if (wait.Elapsed > TimeSpan.FromSeconds(10))
                throw new TimeoutException("Task process cleanup did not complete.");
            await Task.Delay(10, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private uint ActiveProcesses()
    {
        WindowsProcessNative.Require(WindowsProcessNative.QueryInformationJobObject(Handle,
            WindowsProcessNative.JobBasicAccountingInformation,
            out var accounting, (uint)Marshal.SizeOf<WindowsJobAccounting>(), 0));
        return accounting.ActiveProcesses;
    }

    public void Dispose() => Handle.Dispose();
}
