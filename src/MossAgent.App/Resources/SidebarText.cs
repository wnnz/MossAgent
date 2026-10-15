using System.Globalization;
using System.Resources;

namespace MossAgent.App.Resources;

internal static class SidebarText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.SidebarStrings", typeof(SidebarText).Assembly);

    public static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing sidebar resource: {key}");
}
