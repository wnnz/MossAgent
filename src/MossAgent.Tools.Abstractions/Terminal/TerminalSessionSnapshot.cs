namespace MossAgent.Tools.Abstractions.Terminal;

public sealed record TerminalSessionSnapshot(
    Guid TaskId,
    Guid SessionId,
    long Revision,
    TerminalSessionState State,
    int? ExitCode = null,
    TerminalGridSize? Grid = null);
