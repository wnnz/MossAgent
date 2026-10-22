namespace MossAgent.Domain;

public sealed record WorktreeCreationIntent(
    Guid TaskId,
    Guid ProjectId,
    string RepositoryRoot,
    string? BaseReference,
    string TargetPath,
    WorktreeCreationStatus Status,
    string? VolumeId,
    string? ObjectId,
    string? ErrorCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
