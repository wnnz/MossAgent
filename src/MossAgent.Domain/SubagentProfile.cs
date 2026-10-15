namespace MossAgent.Domain;

public sealed record SubagentProfile(
    Guid Id,
    string Name,
    string TriggerCondition,
    string SystemPrompt,
    Guid ProviderId,
    Guid ModelProfileId,
    string ReasoningEffort,
    bool ShareContext,
    bool IsEnabled)
{
    public string StatusLabel => IsEnabled ? "已启用" : "已停用";
}
