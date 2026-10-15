using System.Globalization;
using System.Resources;

namespace MossAgent.App.Resources;

internal static class WindowMenuText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.WindowMenuStrings", typeof(WindowMenuText).Assembly);

    public static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing window menu resource: {key}");
}
