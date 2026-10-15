using MossAgent.Tools.Abstractions;

namespace MossAgent.Application.Agent;

public sealed record AgentToolProgressEvent(
    string ToolName,
    string CallId,
    ToolExecutionPhase Phase) : AgentEvent;
