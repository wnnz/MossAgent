namespace MossAgent.Domain;

public sealed record ConversationFollowupDispatch(
    AgentTask Task,
    ConversationFollowup Followup,
    ConversationMessage Message,
    long QueueRevision);
