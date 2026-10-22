using System.Globalization;
using System.Resources;

namespace MossAgent.App.Resources;

internal static class ComposerInputText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.WorkspaceStrings", typeof(ComposerInputText).Assembly);

    public static string Hint => Get("ComposerInputHint");
    public static string EnterSendsHint => Get("ComposerEnterSendsHint");
    public static string ControlEnterSendsHint => Get("ComposerControlEnterSendsHint");
    public static string EnterSendsShortcut => Get("ComposerEnterSendsShortcut");
    public static string ControlEnterSendsShortcut => Get("ComposerControlEnterSendsShortcut");
    public static string SettingsTitle => Get("ComposerSettingsTitle");
    public static string SettingsDescription => Get("ComposerSettingsDescription");
    public static string EnterSendsOption => Get("ComposerEnterSendsOption");
    public static string ControlEnterSendsOption => Get("ComposerControlEnterSendsOption");
    public static string PreferenceLoadFailed => Get("ComposerPreferenceLoadFailed");
    public static string PreferenceSaveFailed => Get("ComposerPreferenceSaveFailed");
    public static string SubmitLabel => Get("SendRunLabel");
    public static string NewlineLabel => Get("ComposerNewlineLabel");

    public static string HintFor(bool submitOnEnter) =>
        submitOnEnter ? EnterSendsHint : ControlEnterSendsHint;

    public static string ShortcutSummary(bool submitOnEnter) =>
        submitOnEnter ? EnterSendsShortcut : ControlEnterSendsShortcut;

    private static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing composer input resource: {key}");
}
