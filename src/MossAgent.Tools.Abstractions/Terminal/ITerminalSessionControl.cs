namespace MossAgent.Tools.Abstractions.Terminal;

public interface ITerminalSessionControl
{
    Task WriteAsync(Guid taskId, Guid? sessionId, string workingDirectory, string input, CancellationToken cancellationToken);
    Task ResizeAsync(Guid taskId, Guid? sessionId, string workingDirectory, TerminalGridSize size, CancellationToken cancellationToken);
}
