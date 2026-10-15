namespace MossAgent.Domain;

public sealed record ConversationModelPreference(
    Guid ProviderId,
    Guid ModelProfileId,
    string? ReasoningEffort = null,
    Guid? AgentProfileId = null,
    bool FollowAgentReasoning = false);
