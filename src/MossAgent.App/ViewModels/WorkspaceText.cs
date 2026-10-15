using System.Globalization;
using System.Resources;

namespace MossAgent.App.ViewModels;

internal static class WorkspaceText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.WorkspaceStrings", typeof(WorkspaceText).Assembly);

    public static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing workspace resource: {key}");
}
