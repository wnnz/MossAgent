namespace MossAgent.Domain;

public sealed record AgentProfile(
    Guid Id,
    string Name,
    string SystemPrompt,
    Guid ProviderId,
    Guid ModelProfileId,
    string ReasoningEffort,
    bool IsEnabled)
{
    public string StatusLabel => IsEnabled ? "已启用" : "已停用";
}
