using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.App.Tests;

internal sealed class StatisticsGatedProcess(ITaskProcess inner, StatisticsCompletionGate gate,
    bool holdCompletion, bool failExit) : ITaskProcess
{
    public int Id => inner.Id;
    public int ExitCode => failExit ? 128 : inner.ExitCode;
    public TextWriter? StandardInput => inner.StandardInput;
    public TextReader StandardOutput => inner.StandardOutput;
    public TextReader StandardError => inner.StandardError;
    public async Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        await inner.WaitForExitAsync(cancellationToken);
        if (!holdCompletion) return;
        gate.Started.TrySetResult();
        await gate.Release.Task.WaitAsync(cancellationToken);
    }
    public Task TerminateAsync() => inner.TerminateAsync();
    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
