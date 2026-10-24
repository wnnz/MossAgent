namespace MossAgent.Application.Git;

public sealed record WorktreeCleanupIntent(
    Guid TaskId,
    Guid ProjectId,
    string Repository,
    string Target,
    WorktreeCleanupIntentStage Stage,
    string? ErrorCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
