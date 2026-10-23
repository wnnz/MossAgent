namespace MossAgent.Application.Git;

public sealed record WorktreeCreationIntent(
    Guid TaskId,
    Guid ProjectId,
    string Repository,
    string? BaseReference,
    string? BaseCommit,
    string Target,
    WorktreeCreationIntentStage Stage,
    string? ErrorCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
