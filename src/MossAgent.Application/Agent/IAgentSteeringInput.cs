using MossAgent.Domain;

namespace MossAgent.Application.Agent;

public interface IAgentSteeringInput
{
    Guid TaskId { get; }
    Guid RunId { get; }
    Task<ConversationSteeringApplication?> ApplyAtBoundaryAsync(string? precedingAssistant, bool completeIfEmpty,
        Action<IReadOnlyList<ConversationSteeringInput>> validate, CancellationToken cancellationToken);
}
