using System.Diagnostics;
using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Platform.Windows.Processes;

internal sealed class WindowsTaskProcess(
    Process process, WindowsProcessJob job, TextReader standardOutput, TextReader standardError,
    TextWriter? standardInput = null) : ITaskProcess
{
    private bool _disposed;
    public int Id => process.Id;
    public int ExitCode => process.ExitCode;
    public TextWriter? StandardInput => standardInput;
    public TextReader StandardOutput => standardOutput;
    public TextReader StandardError => standardError;

    public Task WaitForExitAsync(CancellationToken cancellationToken) => process.WaitForExitAsync(cancellationToken);

    public async Task TerminateAsync()
    {
        await job.TerminateAsync().ConfigureAwait(false);
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try { await TerminateAsync().ConfigureAwait(false); }
        finally
        {
            try { standardInput?.Dispose(); }
            catch (IOException) { }
            standardOutput.Dispose();
            standardError.Dispose();
            process.Dispose();
            job.Dispose();
        }
    }
}
