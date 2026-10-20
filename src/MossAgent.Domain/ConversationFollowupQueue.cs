namespace MossAgent.Domain;

public sealed record ConversationFollowupQueue(Guid TaskId, long Revision, IReadOnlyList<ConversationFollowup> Items)
{
    public const int MaximumItems = 16;
}
