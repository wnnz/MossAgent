using CodingAgent.Domain.Enums;

namespace CodingAgent.Domain.Models;

/// <summary>LLM 请求。</summary>
public class LlmRequest
{
    public string Model { get; set; } = string.Empty;
    public List<LlmMessage> Messages { get; set; } = [];
    public List<LlmToolDefinition>? Tools { get; set; }
    public ReasoningEffort ReasoningEffort { get; set; } = ReasoningEffort.Off;
    public int MaxOutputTokens { get; set; } = 8192;
}
