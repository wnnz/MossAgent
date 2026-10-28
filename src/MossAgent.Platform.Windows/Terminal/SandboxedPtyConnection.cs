using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MossAgent.Platform.Windows.Processes;
using MossAgent.Platform.Windows.Sandbox;
using MossAgent.Tools.Abstractions.Processes;
using Porta.Pty;

namespace MossAgent.Platform.Windows.Terminal;

/// <summary>
/// <see cref="IPtyConnection"/> backed by a kernel ConPTY whose client process
/// is created suspended inside the task AppContainer (SECURITY_CAPABILITIES),
/// assigned to a kill-on-close job and attached to the pseudoconsole through
/// proc-thread attributes. There is intentionally no unsandboxed fallback:
/// any setup failure surfaces as <see cref="InvalidOperationException"/>.
/// </summary>
internal sealed class SandboxedPtyConnection : IPtyConnection
{
    private readonly WindowsConPty _console;
    private readonly WindowsProcessJob _job;
    private readonly Process _process;
    private readonly WindowsSandboxScope _scope;
    private readonly int _pid;
    private int _exitCode = -1;
    private int _disposed;

    private SandboxedPtyConnection(WindowsConPty console, WindowsProcessJob job, Process process,
        WindowsSandboxScope scope, Stream reader, Stream writer)
    {
        _console = console;
        _job = job;
        _process = process;
        _scope = scope;
        ReaderStream = reader;
        WriterStream = writer;
        _pid = process.Id;
        // The monitor starts before SpawnAsync returns so a fast-exiting shell
        // still closes the pseudoconsole and drains the reader deterministically.
        _ = Task.Run(MonitorExitAsync, CancellationToken.None);
    }

    public event EventHandler<PtyExitedEventArgs>? ProcessExited;
    public Stream ReaderStream { get; }
    public Stream WriterStream { get; }
    public int Pid => _pid;
    public int ExitCode => _process.HasExited ? _process.ExitCode : _exitCode;
    public bool WaitForExit(int milliseconds) => _process.WaitForExit(milliseconds);
    public void Resize(int cols, int rows) => _console.Resize(cols, rows);

    public void Kill()
    {
        try { _job.TerminateAsync().GetAwaiter().GetResult(); }
        catch (Exception) { }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { _job.TerminateAsync().GetAwaiter().GetResult(); }
        catch (Exception) { }
        try { _console.Dispose(); } catch (Exception) { }
        try { ReaderStream.Dispose(); } catch (Exception) { }
        try { WriterStream.Dispose(); } catch (Exception) { }
        try { _process.Dispose(); } catch (Exception) { }
        try { _job.Dispose(); } catch (Exception) { }
        _scope.DisposeAsync().GetAwaiter().GetResult();
    }

    internal static Task<SandboxedPtyConnection> SpawnAsync(PtyOptions options, ProcessIsolationSpec spec,
        WindowsSandboxGrantLedger ledger, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var executable = WindowsExecutableResolver.Resolve(options.App);
        var scope = WindowsSandboxScope.Prepare(spec, executable, ledger);
        try
        {
            return Task.FromResult(Launch(options, executable, scope, cancellationToken));
        }
        catch (Exception failure)
        {
            scope.DisposeAsync().GetAwaiter().GetResult();
            if (failure is OperationCanceledException) throw;
            throw new InvalidOperationException($"沙箱终端启动失败：{failure.Message}", failure);
        }
    }

    private static SandboxedPtyConnection Launch(PtyOptions options, string executable,
        WindowsSandboxScope scope, CancellationToken cancellationToken)
    {
        WindowsConPty? console = WindowsConPty.Create(checked((short)options.Cols), checked((short)options.Rows));
        WindowsProcessJob? job = new WindowsProcessJob();
        Process? process = null;
        Stream? reader = null;
        Stream? writer = null;
        try
        {
            using var attributes = new WindowsConPtyAttributes(job.Handle, console.Handle, scope.SecurityCapabilities);
            using var environment = new WindowsProcessEnvironment(MergeEnvironment(options, scope));
            var information = StartSuspended(options, executable, attributes, environment.Pointer);
            using var processHandle = new SafeFileHandle(information.Process, true);
            using var threadHandle = new SafeFileHandle(information.Thread, true);
            process = Process.GetProcessById(checked((int)information.ProcessId));
            _ = process.Handle;
            reader = console.OpenReader();
            writer = console.OpenWriter();
            cancellationToken.ThrowIfCancellationRequested();
            if (WindowsProcessNative.ResumeThread(threadHandle) == uint.MaxValue)
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            var connection = new SandboxedPtyConnection(console, job, process, scope, reader, writer);
            console = null;
            job = null;
            process = null;
            reader = null;
            writer = null;
            return connection;
        }
        catch
        {
            try { job?.TerminateAsync().GetAwaiter().GetResult(); }
            catch (Exception) { }
            console?.Dispose();
            job?.Dispose();
            process?.Dispose();
            reader?.Dispose();
            writer?.Dispose();
            throw;
        }
    }

    private static WindowsProcessInformation StartSuspended(PtyOptions options, string executable,
        WindowsConPtyAttributes attributes, nint environment)
    {
        var startup = new WindowsStartupInfoEx
        {
            // STARTF_USESTDHANDLES with null standard handles forces the child to
            // bind its stdio to the attached pseudoconsole instead of inheriting
            // this process's handles, which would bypass the ConPTY pipes entirely.
            StartupInfo = new WindowsStartupInfo
            {
                Size = (uint)Marshal.SizeOf<WindowsStartupInfoEx>(),
                Flags = WindowsProcessNative.StartUseStandardHandles
            },
            AttributeList = attributes.Handle
        };
        WindowsProcessNative.Require(WindowsProcessNative.CreateProcess(executable,
            WindowsCommandLine.Create(executable, options.CommandLine), 0, 0, false,
            WindowsProcessNative.ExtendedStartupInfoPresent | WindowsProcessNative.CreateUnicodeEnvironment
                | WindowsProcessNative.CreateSuspended,
            environment, options.Cwd, ref startup, out var information));
        return information;
    }

    private static IReadOnlyDictionary<string, string?> MergeEnvironment(PtyOptions options,
        WindowsSandboxScope scope)
    {
        var merged = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in options.Environment)
            merged[pair.Key] = pair.Value;
        foreach (var pair in scope.EnvironmentOverlay)
            merged[pair.Key] = pair.Value;
        return merged;
    }

    private async Task MonitorExitAsync()
    {
        try
        {
            await Task.Run(() => _process.WaitForExit(), CancellationToken.None).ConfigureAwait(false);
            _exitCode = _process.ExitCode;
            try { ProcessExited?.Invoke(this, CreateExitedArgs(_exitCode)); }
            catch (Exception) { /* A throwing subscriber must not break termination. */ }
        }
        catch (Exception) { }
        finally
        {
            // Ending the pseudoconsole lets conhost exit so ReaderStream drains.
            try { _console.Dispose(); } catch (Exception) { }
        }
    }

    private static PtyExitedEventArgs CreateExitedArgs(int exitCode) =>
        (PtyExitedEventArgs)Activator.CreateInstance(typeof(PtyExitedEventArgs),
            BindingFlags.Instance | BindingFlags.NonPublic, null, [exitCode], null)!;
}
