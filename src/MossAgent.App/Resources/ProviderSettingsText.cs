using System.Globalization;
using System.Resources;

namespace MossAgent.App.Resources;

internal static class ProviderSettingsText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.ProviderSettingsStrings", typeof(ProviderSettingsText).Assembly);

    public static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing provider settings resource: {key}");

    public static string Format(string key, object value) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), value);

    public static string Format(string key, object value, object detail) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), value, detail);
}
