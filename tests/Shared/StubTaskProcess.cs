using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Testing;

internal sealed class StubTaskProcess : ITaskProcess
{
    public int Id => 42;
    public int ExitCode { get; set; }
    public TextWriter? StandardInput { get; set; }
    public TextReader StandardOutput { get; set; } = new StringReader(string.Empty);
    public TextReader StandardError { get; set; } = new StringReader(string.Empty);
    public Func<CancellationToken, Task> Wait { get; set; } = _ => Task.CompletedTask;
    public Func<Task> Terminate { get; set; } = () => Task.CompletedTask;
    public int Terminations { get; private set; }
    public bool Disposed { get; private set; }
    public Task WaitForExitAsync(CancellationToken token) => Wait(token);
    public Task TerminateAsync() { Terminations++; return Terminate(); }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        StandardOutput.Dispose();
        StandardError.Dispose();
        return ValueTask.CompletedTask;
    }
}
