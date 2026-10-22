namespace MossAgent.Domain;

public sealed record WorktreeCleanupIntent(
    Guid TaskId,
    Guid ProjectId,
    string TargetPath,
    string QuarantinePath,
    WorktreeCleanupStage Stage,
    WorktreeCleanupState State,
    string OwnerInstanceId,
    string? ErrorCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
