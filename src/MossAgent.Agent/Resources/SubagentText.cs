using System.Resources;

namespace MossAgent.Agent.Resources;

internal static class SubagentText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.Agent.Resources.SubagentStrings", typeof(SubagentText).Assembly);

    public static string Get(string key) => Labels.GetString(key)
        ?? throw new InvalidOperationException($"Missing subagent resource: {key}");
}
