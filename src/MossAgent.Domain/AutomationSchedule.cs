namespace MossAgent.Domain;

public sealed record AutomationSchedule(
    Guid Id,
    string Name,
    string Prompt,
    Guid ProjectId,
    Guid ProviderId,
    Guid ModelId,
    AutomationFrequency Frequency,
    int IntervalHours,
    int Hour,
    int Minute,
    DayOfWeek Weekday,
    string TimeZoneId,
    ApprovalPolicy ApprovalPolicy,
    int TimeoutMinutes,
    bool IsEnabled,
    DateTimeOffset NextRunAt,
    long Revision = 0,
    AutomationScopeSnapshot? Scope = null,
    AutomationModelSnapshot? Model = null);
