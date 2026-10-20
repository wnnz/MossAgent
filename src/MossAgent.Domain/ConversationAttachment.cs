namespace MossAgent.Domain;

public sealed record ConversationAttachment(
    Guid Id,
    Guid TaskId,
    Guid MessageId,
    string DisplayPath,
    long ByteCount,
    string Sha256,
    string MediaType = "text/plain");
