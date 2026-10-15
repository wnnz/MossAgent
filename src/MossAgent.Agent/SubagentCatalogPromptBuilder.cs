using System.Text;
using MossAgent.Domain;

namespace MossAgent.Agent;

internal static class SubagentCatalogPromptBuilder
{
    public static string Build(IReadOnlyList<SubagentProfile> profiles)
    {
        if (profiles.Count == 0)
        {
            return string.Empty;
        }

        var prompt = new StringBuilder();
        prompt.AppendLine();
        prompt.AppendLine("可用子代理及其适用场景：");
        foreach (var profile in profiles.Take(16))
        {
            var contextMode = profile.ShareContext ? "共享上下文" : "独立上下文";
            prompt.AppendLine($"- {profile.Name}（{contextMode}）：{profile.TriggerCondition}");
        }

        prompt.AppendLine("只有任务符合对应适用场景时，才调用 subagent.run；子代理工具调用沿用主任务授权与审批，且不能再次委派。");
        return prompt.ToString();
    }
}
