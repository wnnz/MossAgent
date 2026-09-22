using CodingAgent.Domain.Enums;

namespace CodingAgent.Domain.Entities;

/// <summary>提供商可用模型及其能力配置。</summary>
public class ProviderModel
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool SupportsTools { get; set; } = true;
    public bool SupportsReasoning { get; set; }
    public ReasoningEffort DefaultReasoningEffort { get; set; }
    public int MaxContextTokens { get; set; } = 128000;
    public int MaxOutputTokens { get; set; } = 8192;
    public bool IsCustom { get; set; }
}
