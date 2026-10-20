using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Agent;

internal sealed record AgentSteeringBoundaryResult(ConversationSteeringApplication Application, IReadOnlyList<ModelMessage> RequiredMessages);
