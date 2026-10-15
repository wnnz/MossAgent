using System.Resources;

namespace MossAgent.App.Resources;

internal static class ProjectlessChatText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.WorkspaceStrings", typeof(ProjectlessChatText).Assembly);

    public static string Label => Labels.GetString("ProjectlessChatLabel")
        ?? throw new InvalidOperationException("Missing projectless chat label.");
}
