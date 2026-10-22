using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Application.Agent;

/// <summary>
/// Result of applying steering inputs at a model boundary.
/// <see cref="Messages"/> are the exact messages appended to the run, including attachment
/// snapshots already verified against the stored hash and the run's model capability.
/// </summary>
public sealed record AgentSteeringApplication(
    ConversationSteeringApplication Applied,
    IReadOnlyList<ModelMessage> Messages);
