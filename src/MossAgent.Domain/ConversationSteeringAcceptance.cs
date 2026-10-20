namespace MossAgent.Domain;

public sealed record ConversationSteeringAcceptance(ConversationSteeringInput Input, long Revision, bool AlreadyAccepted);
