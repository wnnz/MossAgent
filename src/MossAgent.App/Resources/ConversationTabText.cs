using System.Globalization;
using System.Resources;

namespace MossAgent.App.Resources;

public static class ConversationTabText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.WorkspaceStrings", typeof(ConversationTabText).Assembly);
    private static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing conversation tab resource: {key}");
    public static string Options => Get("TabOptionsLabel");
    public static string Close => Get("CloseTabLabel");
}
