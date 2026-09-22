namespace MossAgent.Tools.Abstractions.Terminal;

public interface ITerminalSessionManager : IAsyncDisposable
{
    event EventHandler<TerminalOutputEventArgs>? OutputReceived;
    Task StartAsync(Guid taskId, string workingDirectory, CancellationToken cancellationToken);
    Task WriteAsync(Guid taskId, string input, CancellationToken cancellationToken);
    void Resize(Guid taskId, int columns, int rows);
    Task StopAsync(Guid taskId);
}

