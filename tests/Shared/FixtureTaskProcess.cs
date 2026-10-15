using System.Diagnostics;
using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Testing;

internal sealed class FixtureTaskProcess(Process process) : ITaskProcess
{
    public int Id => process.Id;
    public int ExitCode => process.ExitCode;
    public TextWriter? StandardInput => process.StartInfo.RedirectStandardInput ? process.StandardInput : null;
    public TextReader StandardOutput => process.StandardOutput;
    public TextReader StandardError => process.StandardError;
    public Task WaitForExitAsync(CancellationToken token) => process.WaitForExitAsync(token);

    public async Task TerminateAsync()
    {
        if (!process.HasExited) process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync(CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        try { await TerminateAsync(); }
        finally { process.Dispose(); }
    }
}
