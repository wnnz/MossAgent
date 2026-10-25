namespace MossAgent.Tools.Abstractions.Files;

/// <summary>One persisted row of the artifact/diff-input session journal.</summary>
public sealed record ArtifactSessionRecord(
    long Id,
    Guid TaskId,
    string FileName,
    string FullPath,
    ArtifactSessionKind Kind,
    ArtifactSessionStage Stage,
    long? ByteCount,
    string? ErrorCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
