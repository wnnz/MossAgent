using System.Globalization;
using System.Resources;

namespace MossAgent.Tools.BuiltIn.Resources;

internal static class ToolExecutionText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.Tools.BuiltIn.Resources.ToolExecutionStrings", typeof(ToolExecutionText).Assembly);

    public static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing tool execution resource: {key}");

    public static string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
}
