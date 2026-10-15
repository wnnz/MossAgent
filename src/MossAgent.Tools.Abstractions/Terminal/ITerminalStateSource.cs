namespace MossAgent.Tools.Abstractions.Terminal;

public interface ITerminalStateSource
{
    event EventHandler<TerminalStateEventArgs>? StateChanged;
    TerminalSessionSnapshot? GetSnapshot(Guid taskId);
}
