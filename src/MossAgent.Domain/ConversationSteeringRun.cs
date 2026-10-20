namespace MossAgent.Domain;

public sealed record ConversationSteeringRun(
    Guid Id,
    Guid TaskId,
    long Revision,
    DateTimeOffset StartedAt,
    DateTimeOffset? ClosedAt = null)
{
    public bool IsOpen => ClosedAt is null;
}
