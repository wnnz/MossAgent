using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Platform.Windows.Processes;

public sealed class WindowsTaskProcessLauncher : ITaskProcessLauncher
{
    public ITaskProcess Start(TaskProcessStartRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkingDirectory);
        if (!Path.IsPathFullyQualified(request.WorkingDirectory))
            throw new ArgumentException("Process working directory must be absolute.", nameof(request));
        request = request with { WorkingDirectory = Path.GetFullPath(request.WorkingDirectory) };
        var executable = WindowsExecutableResolver.Resolve(request.FileName);
        using var environment = new WindowsProcessEnvironment(request.Environment, request.RemovedEnvironmentPrefixes);
        using var standardOutput = WindowsProcessPipe.Create();
        using var standardError = WindowsProcessPipe.Create();
        using var standardInput = WindowsProcessPipe.Create(childReads: true);
        if (!request.RedirectStandardInput) standardInput.CloseWriter();
        var job = new WindowsProcessJob();
        Process? process = null;
        TextReader? output = null;
        TextReader? error = null;
        TextWriter? input = null;
        try
        {
            using var attributes = new WindowsProcessAttributes(job.Handle,
                standardInput.ReadHandle, standardOutput.WriteHandle, standardError.WriteHandle);
            var information = StartSuspended(request, executable, attributes,
                standardInput, standardOutput, standardError, environment.Pointer);
            using var nativeProcess = new SafeFileHandle(information.Process, true);
            using var thread = new SafeFileHandle(information.Thread, true);
            process = Process.GetProcessById(checked((int)information.ProcessId));
            _ = process.Handle;
            output = standardOutput.OpenReader();
            error = standardError.OpenReader();
            if (request.RedirectStandardInput) input = standardInput.OpenWriter();
            cancellationToken.ThrowIfCancellationRequested();
            if (WindowsProcessNative.ResumeThread(thread) == uint.MaxValue)
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            return new WindowsTaskProcess(process, job, output, error, input);
        }
        catch
        {
            try { job.TerminateAsync().GetAwaiter().GetResult(); }
            finally { job.Dispose(); process?.Dispose(); output?.Dispose(); error?.Dispose(); input?.Dispose(); }
            throw;
        }
    }

    private static WindowsProcessInformation StartSuspended(TaskProcessStartRequest request, string executable,
        WindowsProcessAttributes attributes, WindowsProcessPipe input, WindowsProcessPipe output,
        WindowsProcessPipe error, nint environment)
    {
        var startup = new WindowsStartupInfoEx
        {
            StartupInfo = new WindowsStartupInfo
            {
                Size = (uint)Marshal.SizeOf<WindowsStartupInfoEx>(), Flags = WindowsProcessNative.StartUseStandardHandles,
                StandardInput = input.ReadHandle, StandardOutput = output.WriteHandle, StandardError = error.WriteHandle
            },
            AttributeList = attributes.Handle
        };
        WindowsProcessNative.Require(WindowsProcessNative.CreateProcess(executable,
            WindowsCommandLine.Create(executable, request.Arguments), 0, 0, true,
            WindowsProcessNative.CreateSuspended | WindowsProcessNative.CreateNoWindow
                | WindowsProcessNative.ExtendedStartupInfoPresent | WindowsProcessNative.CreateUnicodeEnvironment,
            environment, request.WorkingDirectory, ref startup, out var information));
        return information;
    }
}
