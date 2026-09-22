using MossAgent.Tools.Abstractions;

namespace MossAgent.Application.Agent;

public sealed record AgentToolEvent(
    string ToolName,
    string CallId,
    ToolResult Result) : AgentEvent;

