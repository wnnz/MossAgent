namespace MossAgent.Domain;

public sealed record ConversationSteeringPending(ConversationSteeringRun Run, IReadOnlyList<ConversationSteeringInput> Inputs);
