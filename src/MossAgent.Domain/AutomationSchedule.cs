using System.Text.Json.Serialization;

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
    bool FollowAuthorizationChanges = false)
{
    /// <summary>Pinned authorization captured at creation/edit time. Persisted in a dedicated
    /// store next to the schedule payload, never inside the serialized payload itself.</summary>
    [JsonIgnore]
    public AutomationAuthorizationSnapshot? AuthorizationSnapshot { get; init; }
}
