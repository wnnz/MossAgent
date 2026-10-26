using System.Globalization;
using System.Resources;

namespace MossAgent.App.Resources;

public static class AutomationText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.AutomationStrings", typeof(AutomationText).Assembly);

    public static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing automation resource: {key}");
    public static string Format(string key, params object[] values) => string.Format(CultureInfo.CurrentCulture, Get(key), values);
    public static string Title => Get("Title");
    public static string Subtitle => Get("Subtitle");
    public static string Create => Get("Create");
    public static string EmptyTitle => Get("EmptyTitle");
    public static string EmptyHint => Get("EmptyHint");
    public static string Refresh => Get("Refresh");
    public static string ListLoading => Get("ListLoading");
    public static string ListLoadingHint => Get("ListLoadingHint");
    public static string ListLoadFailed => Get("ListLoadFailed");
    public static string Edit => Get("Edit");
    public static string RunNow => Get("RunNow");
    public static string Stop => Get("Stop");
    public static string Remove => Get("Remove");
    public static string Name => Get("Name");
    public static string Prompt => Get("Prompt");
    public static string Project => Get("Project");
    public static string Model => Get("Model");
    public static string Frequency => Get("Frequency");
    public static string Time => Get("Time");
    public static string Weekday => Get("Weekday");
    public static string Interval => Get("Interval");
    public static string TimeZone => Get("TimeZone");
    public static string Policy => Get("Policy");
    public static string Timeout => Get("Timeout");
    public static string Save => Get("Save");
    public static string Cancel => Get("Cancel");
    public static string History => Get("History");
    public static string HistoryLimit => Get("HistoryLimit");
    public static string HistoryLoading => Get("HistoryLoading");
    public static string Retry => Get("Retry");
    public static string CancelOperation => Get("CancelOperation");
    public static string NoRuns => Get("NoRuns");
    public static string SelectSchedule => Get("SelectSchedule");
    public static string OpenConversation => Get("OpenConversation");
    public static string DeleteTitle => Get("DeleteTitle");
    public static string DeleteHint => Get("DeleteHint");
    public static string PausedDefault => Get("PausedDefault");
    public static string FollowAuthorization => Get("FollowAuthorization");
    public static string FollowAuthorizationHint => Get("FollowAuthorizationHint");
    public static string FollowsAuthorization => Get("FollowsAuthorization");
    public static string SnapshotHint => Get("SnapshotHint");
}
