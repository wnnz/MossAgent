using System.Globalization;
using System.Resources;

namespace MossAgent.App.Resources;

internal static class SettingsText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.SettingsStrings", typeof(SettingsText).Assembly);

    public static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing settings resource: {key}");
}
