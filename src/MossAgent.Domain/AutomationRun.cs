namespace MossAgent.Domain;

public sealed record AutomationRun(
    Guid Id,
    Guid ScheduleId,
    Guid TaskId,
    DateTimeOffset StartedAt,
    AutomationRunStatus Status,
    bool IsManual,
    DateTimeOffset? FinishedAt = null,
    string? ErrorCode = null);
