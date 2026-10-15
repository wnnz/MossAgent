using System.Globalization;
using System.Resources;

namespace MossAgent.App.Resources;

internal static class NavigationRailText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.NavigationRailStrings", typeof(NavigationRailText).Assembly);

    public static string Settings => Get("Settings");

    public static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing navigation rail resource: {key}");
}
