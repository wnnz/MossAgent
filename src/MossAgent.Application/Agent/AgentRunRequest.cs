using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Application.Agent;

public sealed record AgentRunRequest(
    AiProvider Provider,
    ModelProfile Model,
    IReadOnlyList<ModelMessage> Messages,
    ToolExecutionContext ToolContext,
    int MaximumTurns = 20);

