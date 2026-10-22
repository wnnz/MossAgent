using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

/// <summary>
/// Idempotent identity of one steering submission attempt for a task.
/// <paramref name="Id"/> doubles as the steering receipt id and the attachment snapshot owner id, so a
/// retry reuses the same snapshots instead of creating a second set of files.
/// </summary>
/// <param name="RunId">Run identity the attempt was bound to.</param>
/// <param name="Scope">Draft scope the text and attachments were captured from.</param>
/// <param name="Text">Captured steering text.</param>
/// <param name="Id">Stable steering input identity for this attempt.</param>
/// <param name="AttachmentIds">Composer attachment identities captured with this attempt.</param>
/// <param name="Attachments">Snapshots written for this attempt, or null before the first snapshot write.</param>
internal sealed record WorkspaceSteeringAttempt(Guid RunId, ConversationDraftScope Scope, string Text, Guid Id,
    IReadOnlyList<Guid> AttachmentIds, IReadOnlyList<ConversationAttachment>? Attachments = null)
{
    /// <summary>
    /// Checks whether a new composer state is the same submission attempt, so a retry reuses the
    /// receipt identity and the snapshots already written for it.
    /// </summary>
    /// <param name="runId">Active run identity.</param>
    /// <param name="scope">Current draft scope.</param>
    /// <param name="text">Current steering text.</param>
    /// <param name="attachmentIds">Current composer attachment identities in display order.</param>
    public bool Matches(Guid runId, ConversationDraftScope scope, string text, IReadOnlyList<Guid> attachmentIds) =>
        RunId == runId && Scope == scope && Text == text && AttachmentIds.SequenceEqual(attachmentIds);
}
