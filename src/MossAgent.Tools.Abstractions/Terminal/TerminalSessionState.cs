namespace MossAgent.Tools.Abstractions.Terminal;

public enum TerminalSessionState
{
    Starting,
    Running,
    Stopping,
    Stopped,
    Exited,
    Failed,
    CleanupFailed
}
