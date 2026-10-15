using System.Globalization;

namespace MossAgent.App.Resources;

public static class SettingsItemLabels
{
    public static string Test => SettingsText.Get("RowTest");
    public static string Edit => SettingsText.Get("RowEdit");
    public static string Enable => SettingsText.Get("RowEnable");
    public static string Disable => SettingsText.Get("RowDisable");
    public static string Default => SettingsText.Get("RowDefault");

    internal static string ActionName(string? action, string? title) => string.IsNullOrWhiteSpace(action)
        ? string.Empty : string.Format(CultureInfo.CurrentCulture, SettingsText.Get("RowActionName"), action, title ?? string.Empty);
}
