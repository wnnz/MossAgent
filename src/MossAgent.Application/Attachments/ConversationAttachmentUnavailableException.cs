namespace MossAgent.Application.Attachments;

public sealed class ConversationAttachmentUnavailableException()
    : IOException("Conversation attachment history is unavailable.");
