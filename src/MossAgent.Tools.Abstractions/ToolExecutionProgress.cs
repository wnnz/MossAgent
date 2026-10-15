namespace MossAgent.Tools.Abstractions;

public sealed record ToolExecutionProgress(
    Guid TaskId,
    string CallId,
    string ToolName,
    ToolExecutionPhase Phase);
