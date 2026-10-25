namespace MossAgent.Infrastructure.Attachments;

internal sealed record AttachmentReclaimCandidate(
    string Path,
    AttachmentCandidateKind Kind,
    Guid? TaskId,
    Guid? ContainerId,
    Guid? AttachmentId,
    string? ScopeHash,
    string DirectoryPath,
    long Length,
    DateTimeOffset LastWriteUtc);
