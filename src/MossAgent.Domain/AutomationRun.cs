namespace MossAgent.Domain;

/// <param name="SnapshotGeneration">Authorization snapshot generation pinned at claim time;
/// null on rows written before provenance tracking or claimed without a snapshot.</param>
public sealed record AutomationRun(
    Guid Id,
    Guid ScheduleId,
    Guid TaskId,
    DateTimeOffset StartedAt,
    AutomationRunStatus Status,
    bool IsManual,
    DateTimeOffset? FinishedAt = null,
    string? ErrorCode = null,
    long? SnapshotGeneration = null);
