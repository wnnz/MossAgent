using System.Globalization;

namespace MossAgent.App.ViewModels;

internal static class ConversationTimelineText
{
    public static string Timestamp(DateTimeOffset? recordedAt, DateTimeOffset now, TimeZoneInfo? timeZone = null)
    {
        if (recordedAt is null) return string.Empty;
        var zone = timeZone ?? TimeZoneInfo.Local;
        var local = TimeZoneInfo.ConvertTime(recordedAt.Value, zone);
        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        var key = local.Date == today ? "TimelineToday"
            : local.Date == today.AddDays(-1) ? "TimelineYesterday" : "TimelineDate";
        return string.Format(CultureInfo.CurrentCulture, WorkspaceText.Get(key), local);
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
