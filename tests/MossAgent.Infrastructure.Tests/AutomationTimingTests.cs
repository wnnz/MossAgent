using MossAgent.Application.Automations;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AutomationTimingTests
{
    private static AutomationSchedule Schedule() => new(Guid.NewGuid(), "计划", "提示词", Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), AutomationFrequency.Daily, 1, 9, 0, DayOfWeek.Monday, "UTC", ApprovalPolicy.ReadOnly, 30, true, default);

    [Theory]
    [InlineData("2026-10-05T08:59:00Z", "2026-10-05T09:00:00Z")]
    [InlineData("2026-10-05T09:00:00Z", "2026-10-06T09:00:00Z")]
    [InlineData("2026-10-05T09:01:00Z", "2026-10-06T09:00:00Z")]
    public void Daily_IsStrictlyFuture(string after, string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected), AutomationTiming.Next(Schedule(), DateTimeOffset.Parse(after)));

    [Fact]
    public void WeeklyAndHourly_SkipMissedIntervals()
    {
        var after = DateTimeOffset.Parse("2026-10-05T10:00:00Z");
        Assert.Equal(after.AddHours(3), AutomationTiming.Next(Schedule() with { Frequency = AutomationFrequency.Hourly, IntervalHours = 3 }, after));
        Assert.Equal(DateTimeOffset.Parse("2026-10-12T09:00:00Z"), AutomationTiming.Next(Schedule() with { Frequency = AutomationFrequency.Weekly }, after));
    }

    [Fact]
    public void SpringGap_AdvancesToValidTime()
    {
        var schedule = Schedule() with { TimeZoneId = "America/New_York", Hour = 2, Minute = 30 };
        Assert.Equal(DateTimeOffset.Parse("2026-03-08T07:00:00Z"),
            AutomationTiming.Next(schedule, DateTimeOffset.Parse("2026-03-08T05:00:00Z")));
    }

    [Fact]
    public void FallOverlap_UsesOnlyLaterOccurrence()
    {
        var schedule = Schedule() with { TimeZoneId = "America/New_York", Hour = 1, Minute = 30 };
        var next = AutomationTiming.Next(schedule, DateTimeOffset.Parse("2026-11-01T04:00:00Z"));
        Assert.Equal(DateTimeOffset.Parse("2026-11-01T06:30:00Z"), next);
        Assert.Equal(DateTimeOffset.Parse("2026-11-02T06:30:00Z"), AutomationTiming.Next(schedule, next));
    }

    [Theory]
    [InlineData(-1, 0, 1, 30)]
    [InlineData(24, 0, 1, 30)]
    [InlineData(9, 60, 1, 30)]
    [InlineData(9, 0, 0, 30)]
    [InlineData(9, 0, 1, 241)]
    public void InvalidTiming_IsRejected(int hour, int minute, int interval, int timeout) =>
        Assert.Throws<ArgumentException>(() => AutomationTiming.Next(Schedule() with
            { Hour = hour, Minute = minute, IntervalHours = interval, TimeoutMinutes = timeout }, DateTimeOffset.UtcNow));

    [Fact]
    public void UnknownZoneAndPermissions_AreRejected()
    {
        Assert.Throws<TimeZoneNotFoundException>(() => AutomationTiming.Validate(Schedule() with { TimeZoneId = "not-a-zone" }));
        Assert.Throws<ArgumentException>(() => AutomationTiming.Validate(Schedule() with { ApprovalPolicy = (ApprovalPolicy)9 }));
    }
}
