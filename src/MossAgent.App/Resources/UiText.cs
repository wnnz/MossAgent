using System.Globalization;
using System.Resources;

namespace MossAgent.App.Resources;

/// <summary>
/// gettext-style user-facing text lookup: the source literal is the message id,
/// an optional UiStrings resource overrides it per locale. Missing entries fall
/// back to the literal so the UI always renders the authored text.
/// </summary>
public static class UiText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.UiStrings", typeof(UiText).Assembly);

    public static string T(string msgid) =>
        Labels.GetString(msgid, CultureInfo.CurrentUICulture) ?? msgid;

    public static string T(string msgid, params object?[] args) => string.Format(T(msgid), args);
}
