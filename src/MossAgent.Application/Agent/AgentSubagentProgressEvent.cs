using MossAgent.Tools.Abstractions;

namespace MossAgent.Application.Agent;

public sealed record AgentSubagentProgressEvent(
    string CallId,
    ToolDialogueMessage Message) : AgentEvent;
