using MossAgent.Domain;

namespace MossAgent.Application.Conversations;

public sealed record PreparedConversationFollowup(
    AgentTask Task,
    ProjectProfile? Project,
    AiProvider Provider,
    ModelProfile Model,
    ConversationFollowup Followup,
    long QueueRevision);
