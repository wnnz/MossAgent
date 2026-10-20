namespace MossAgent.Application.Conversations;

public sealed class ConversationSteeringException(string code) : InvalidOperationException("The active run cannot accept this input.")
{
    public string Code { get; } = code;
}
