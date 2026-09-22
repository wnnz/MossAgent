namespace MossAgent.Domain;

public sealed record ConversationMessage(
    Guid Id,
    Guid TaskId,
    MessageRole Role,
    string Content,
    DateTimeOffset CreatedAt,
    string? ToolCallId = null);
