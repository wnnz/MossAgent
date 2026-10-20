namespace MossAgent.Application.Conversations;

public sealed class ConversationFollowupException(string code) : Exception("The queued message could not be prepared.")
{
    public string Code { get; } = code;
}
