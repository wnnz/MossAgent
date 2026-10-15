using System.Globalization;
using System.Resources;

namespace MossAgent.Tools.BuiltIn.Resources;

internal static class ToolValidationText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.Tools.BuiltIn.Resources.ToolValidationStrings", typeof(ToolValidationText).Assembly);

    public static string Format(string key, params object[] values) => string.Format(
        CultureInfo.CurrentUICulture,
        Labels.GetString(key, CultureInfo.CurrentUICulture)
            ?? throw new InvalidOperationException($"Missing tool validation resource: {key}"), values);
}
