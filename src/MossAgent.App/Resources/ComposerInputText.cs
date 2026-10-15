using System.Globalization;
using System.Resources;

namespace MossAgent.App.Resources;

internal static class ComposerInputText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.WorkspaceStrings", typeof(ComposerInputText).Assembly);

    public static string Hint => Get("ComposerInputHint");
    public static string SubmitLabel => Get("SendRunLabel");
    public static string NewlineLabel => Get("ComposerNewlineLabel");

    private static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing composer input resource: {key}");
}
