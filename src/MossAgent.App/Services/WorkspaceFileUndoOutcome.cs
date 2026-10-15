using MossAgent.Domain;

namespace MossAgent.App.Services;

public sealed record WorkspaceFileUndoOutcome(string StatusKey, string? Detail = null,
    ConversationMessage? UnsavedRecord = null);
