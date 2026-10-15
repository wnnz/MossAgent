namespace MossAgent.Tools.Abstractions.Terminal;

public sealed class TerminalStateEventArgs(TerminalSessionSnapshot snapshot) : EventArgs
{
    public TerminalSessionSnapshot Snapshot { get; } = snapshot;
}
