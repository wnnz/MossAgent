using System.Text.Json.Serialization;

namespace MossAgent.Domain;

public sealed record SubagentRun(
    Guid Id,
    Guid TaskId,
    Guid ParentRunId,
    string ParentCallId,
    [property: JsonIgnore] Guid ProfileId,
    string AgentName,
    [property: JsonIgnore] string TriggerCondition,
    [property: JsonIgnore] string SystemPrompt,
    [property: JsonIgnore] Guid ProviderId,
    string ProviderName,
    ProviderProtocol ProviderProtocol,
    [property: JsonIgnore] Guid ModelProfileId,
    string ModelId,
    [property: JsonIgnore] int ContextLength,
    [property: JsonIgnore] int MaximumOutputTokens,
    string ReasoningEffort,
    bool ShareContext,
    [property: JsonIgnore] string OwnerInstanceId,
    SubagentRunStatus Status,
    string? Summary,
    string? ErrorCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt)
{
    [JsonIgnore]
    public string StatusLabel => Status switch
    {
        SubagentRunStatus.Running => "运行中",
        SubagentRunStatus.Completed => "已完成",
        SubagentRunStatus.Failed => "失败",
        SubagentRunStatus.Cancelled => "已取消",
        _ => "已中断"
    };

    [JsonIgnore]
    public string DisplayMetadata => $"{ProviderName} / {ModelId} · {ReasoningEffort}";

    [JsonIgnore]
    public string Reference => Id.ToString("N")[..8];
}
