namespace MossAgent.Domain;

public sealed record ConversationSteeringApplication(
    ConversationSteeringRun Run,
    IReadOnlyList<ConversationSteeringInput> Inputs,
    IReadOnlyList<ConversationMessage> Messages,
    ConversationMessage? PrecedingAssistant);
