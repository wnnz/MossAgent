using System.Globalization;
using System.Resources;

namespace MossAgent.App.Resources;

internal static class ConversationHeaderText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.WorkspaceStrings", typeof(ConversationHeaderText).Assembly);

    public static string MoreLabel => Get("ConversationMoreLabel");
    public static string ContextLabel => Get("ConversationContextLabel");

    private static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing conversation header resource: {key}");
}
