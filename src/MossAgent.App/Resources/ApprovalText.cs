using System.Globalization;
using System.Resources;

namespace MossAgent.App.Resources;

public static class ApprovalText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.ApprovalStrings", typeof(ApprovalText).Assembly);

    public static string Title => Get("Title");
    public static string Deny => Get("Deny");
    public static string Approve => Get("Approve");
    public static string StopTask => Get("StopTask");
    public static string Directory => Get("Directory");
    public static string Details => Get("Details");
    public static string PrivacyHint => Get("PrivacyHint");
    public static string PreviousContent => Get("PreviousContent");
    public static string NextContent => Get("NextContent");

    internal static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing approval resource: {key}");

    internal static string Format(string key, params object[] values) =>
        string.Format(CultureInfo.CurrentUICulture, Get(key), values);
}
