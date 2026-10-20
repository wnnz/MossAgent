namespace MossAgent.Application.Persistence;

public sealed class ConversationDraftConflictException()
    : InvalidOperationException("The conversation draft changed before submission.");
