using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Application.Agent;

public interface IAgentSteeringInput
{
    Guid TaskId { get; }
    Guid RunId { get; }

    /// <summary>
    /// Applies the pending inputs that are about to reach the model.
    /// </summary>
    /// <param name="precedingAssistant">Assistant text completed at this boundary, or null for the first boundary.</param>
    /// <param name="completeIfEmpty">
    /// When true the run is closed if no input is pending; late inputs are then refused instead of applied.
    /// </param>
    /// <param name="validate">
    /// Receives the exact messages appended to the run so the caller can check the model context budget
    /// before anything is committed. Attachment snapshots are already read and verified at this point.
    /// </param>
    /// <param name="cancellationToken">Run-scoped cancellation token.</param>
    /// <returns>The committed application, or null when the run closed the boundary without any input.</returns>
    /// <exception cref="Conversations.ConversationSteeringException">
    /// Thrown when the run stopped accepting input, the stored attachment snapshot is unusable
    /// (<c>SteeringAttachmentsUnavailable</c>), or the committed prefix changed concurrently.
    /// </exception>
    Task<AgentSteeringApplication?> ApplyAtBoundaryAsync(string? precedingAssistant, bool completeIfEmpty,
        Action<IReadOnlyList<ModelMessage>> validate, CancellationToken cancellationToken);
}
