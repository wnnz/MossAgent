using System.Globalization;
using System.Resources;

namespace MossAgent.Browser.Resources;

internal static class BrowserToolText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.Browser.Resources.BrowserToolStrings", typeof(BrowserToolText).Assembly);

    public static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing browser resource: {key}");
}
