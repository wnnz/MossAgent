using System.Globalization;

namespace MossAgent.App.ViewModels;

internal static class ConversationTimelineText
{
    public static string Timestamp(DateTimeOffset? recordedAt, DateTimeOffset now, TimeZoneInfo? timeZone = null)
    {
        if (recordedAt is null) return string.Empty;
        var zone = timeZone ?? TimeZoneInfo.Local;
        return FormatTimestamp(recordedAt.Value,
            TimestampKey(TimeZoneInfo.ConvertTime(recordedAt.Value, zone).Date, Today(now, zone)), zone);
    }

    /// <summary>Today's date in <paramref name="timeZone"/> — the only per-refresh input that
    /// decides which timestamp template applies, so callers can cache the formatted output
    /// until this value or the recorded date changes.</summary>
    public static DateTime Today(DateTimeOffset now, TimeZoneInfo? timeZone = null) =>
        TimeZoneInfo.ConvertTime(now, timeZone ?? TimeZoneInfo.Local).Date;

    /// <summary>Template selector for a recorded local date: 0 today, 1 yesterday, 2 any
    /// other date. Together with the recorded instant it fully determines the formatted
    /// timestamp, so equal keys mean the cached string can be reused.</summary>
    public static int TimestampKey(DateTime localDate, DateTime today) =>
        localDate == today ? 0 : localDate == today.AddDays(-1) ? 1 : 2;

    public static string FormatTimestamp(DateTimeOffset recordedAt, int key, TimeZoneInfo? timeZone = null)
    {
        var local = TimeZoneInfo.ConvertTime(recordedAt, timeZone ?? TimeZoneInfo.Local);
        var resource = key switch { 0 => "TimelineToday", 1 => "TimelineYesterday", _ => "TimelineDate" };
        return string.Format(CultureInfo.CurrentCulture, WorkspaceText.Get(resource), local);
    }

    public static string Elapsed(TimeSpan elapsed)
    {
        var seconds = (long)elapsed.TotalSeconds;
        if (seconds < 60) return string.Format(CultureInfo.CurrentCulture, WorkspaceText.Get("TimelineSeconds"), seconds);
        if (seconds < 3600) return string.Format(CultureInfo.CurrentCulture, WorkspaceText.Get("TimelineMinutes"),
            seconds / 60, seconds % 60);
        return string.Format(CultureInfo.CurrentCulture, WorkspaceText.Get("TimelineHours"),
            seconds / 3600, seconds / 60 % 60, seconds % 60);
    }
}
