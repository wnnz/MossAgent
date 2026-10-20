using MossAgent.Domain;

namespace MossAgent.Infrastructure.Attachments;

internal sealed record ConversationDraftAttachmentMetadata(ConversationDraftScope Scope,
    ConversationDraftAttachment Attachment, string Sha256);
