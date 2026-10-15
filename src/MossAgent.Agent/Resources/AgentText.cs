using System.Globalization;
using System.Resources;

namespace MossAgent.Agent.Resources;

internal static class AgentText
{
    private static readonly ResourceManager Labels = new("MossAgent.Agent.Resources.AgentStrings", typeof(AgentText).Assembly);

    public static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing agent resource: {key}");
}
