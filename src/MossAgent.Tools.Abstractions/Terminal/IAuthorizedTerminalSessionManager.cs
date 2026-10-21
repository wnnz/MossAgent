namespace MossAgent.Tools.Abstractions.Terminal;

public interface IAuthorizedTerminalSessionManager
{
    Task StartAuthorizedAsync(ToolExecutionContext context, CancellationToken cancellationToken);
}
