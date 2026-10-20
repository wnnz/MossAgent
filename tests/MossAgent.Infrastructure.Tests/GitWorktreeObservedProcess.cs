using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Infrastructure.Tests;

internal sealed class GitWorktreeObservedProcess(ITaskProcess inner, Func<Task> afterExit) : ITaskProcess
{
    public int Id => inner.Id;
    public int ExitCode => inner.ExitCode;
    public TextWriter? StandardInput => inner.StandardInput;
    public TextReader StandardOutput => inner.StandardOutput;
    public TextReader StandardError => inner.StandardError;

    public async Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        await inner.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        await afterExit().ConfigureAwait(false);
    }

    public Task TerminateAsync() => inner.TerminateAsync();
    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
