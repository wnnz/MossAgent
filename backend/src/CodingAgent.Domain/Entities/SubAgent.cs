using CodingAgent.Domain.Enums;

namespace CodingAgent.Domain.Entities;

/// <summary>子代理配置：独立 AgentLoop，由主代理按调用规则决策触发。</summary>
public class SubAgent
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InvocationRule { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public int ProviderId { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public ReasoningEffort ReasoningEffort { get; set; }
    public int MaxTurns { get; set; } = 10;
    /// <summary>JSON 数组，允许使用的工具名列表。</summary>
    public string AllowedToolsJson { get; set; } = "[]";
    public bool Enabled { get; set; } = true;
}
