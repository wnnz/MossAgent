using MossAgent.Domain;

namespace MossAgent.Application.Automations;

public static class AutomationTiming
{
    public static void Validate(AutomationSchedule schedule)
    {
        if (schedule.Id == Guid.Empty || schedule.ProjectId == Guid.Empty
            || schedule.ProviderId == Guid.Empty || schedule.ModelId == Guid.Empty
            || string.IsNullOrWhiteSpace(schedule.Name) || schedule.Name.Length > 120
            || string.IsNullOrWhiteSpace(schedule.Prompt) || schedule.Prompt.Length > 32_000
            || !Enum.IsDefined(schedule.Frequency) || !Enum.IsDefined(schedule.Weekday)
            || !Enum.IsDefined(schedule.ApprovalPolicy) || schedule.Hour is < 0 or > 23
            || schedule.Minute is < 0 or > 59 || schedule.IntervalHours is < 1 or > 24
            || schedule.TimeoutMinutes is < 1 or > 240 || schedule.Revision < 0
            || string.IsNullOrWhiteSpace(schedule.TimeZoneId))
            throw new ArgumentException("Invalid automation configuration.");
        AutomationScopeRules.Validate(schedule);
        _ = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId);
    }

    public static DateTimeOffset Next(AutomationSchedule schedule, DateTimeOffset after)
    {
        Validate(schedule);
        if (schedule.Frequency == AutomationFrequency.Hourly)
            return after.ToUniversalTime().AddHours(schedule.IntervalHours);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId);
        var local = TimeZoneInfo.ConvertTime(after, zone).Date;
        for (var offset = 0; offset <= 8; offset++)
        {
            var date = local.AddDays(offset);
            if (schedule.Frequency == AutomationFrequency.Weekly && date.DayOfWeek != schedule.Weekday) continue;
            var candidate = date.AddHours(schedule.Hour).AddMinutes(schedule.Minute);
            while (zone.IsInvalidTime(candidate)) candidate = candidate.AddMinutes(1);
            var utcOffset = zone.IsAmbiguousTime(candidate)
                ? zone.GetAmbiguousTimeOffsets(candidate).Min()
                : zone.GetUtcOffset(candidate);
            var occurrence = new DateTimeOffset(candidate, utcOffset).ToUniversalTime();
            if (occurrence > after) return occurrence;
        }
        throw new InvalidOperationException("No future automation occurrence.");
    }
}
