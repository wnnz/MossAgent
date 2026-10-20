namespace MossAgent.Domain;

public sealed record ConversationSteeringPage(IReadOnlyList<ConversationSteeringInput> Items, bool HasMore);
