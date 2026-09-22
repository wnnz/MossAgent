namespace MossAgent.Tools.Abstractions.Terminal;

public sealed class TerminalOutputEventArgs(Guid taskId, string output) : EventArgs
{
    public Guid TaskId { get; } = taskId;
    public string Output { get; } = output;
}

