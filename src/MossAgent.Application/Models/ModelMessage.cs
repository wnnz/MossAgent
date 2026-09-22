namespace MossAgent.Application.Models;

public sealed record ModelMessage(
    ModelRole Role,
    string Content,
    string? ToolCallId = null,
    string? ToolName = null,
    IReadOnlyList<ModelToolCall>? ToolCalls = null);
