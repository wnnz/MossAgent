namespace MossAgent.Application.Agent;

public sealed record AgentFailureEvent(string Code, string Message) : AgentEvent;

