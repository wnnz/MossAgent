using System.Globalization;
using System.Resources;

namespace MossAgent.Tools.BuiltIn.Resources;

internal static class FileToolText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.Tools.BuiltIn.Resources.FileToolStrings", typeof(FileToolText).Assembly);

    public static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing file tool resource: {key}");

    public static string Format(string key, params object[] values) =>
        string.Format(CultureInfo.CurrentUICulture, Get(key), values);
}
