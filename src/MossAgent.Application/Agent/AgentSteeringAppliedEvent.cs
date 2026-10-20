using MossAgent.Domain;

namespace MossAgent.Application.Agent;

public sealed record AgentSteeringAppliedEvent(ConversationSteeringApplication Application) : AgentEvent;
