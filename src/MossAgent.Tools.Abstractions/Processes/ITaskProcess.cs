namespace MossAgent.Tools.Abstractions.Processes;

public interface ITaskProcess : IAsyncDisposable
{
    int Id { get; }
    int ExitCode { get; }
    TextWriter? StandardInput { get; }
    TextReader StandardOutput { get; }
    TextReader StandardError { get; }
    Task WaitForExitAsync(CancellationToken cancellationToken);
    Task TerminateAsync();
}
