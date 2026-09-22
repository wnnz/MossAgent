using CodingAgent.Domain.Enums;

namespace CodingAgent.Domain.Entities;

/// <summary>AI 提供商。</summary>
public class Provider
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProviderType Type { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int? ProxyId { get; set; }
    public bool Enabled { get; set; } = true;
    public List<ProviderModel> Models { get; set; } = [];
}
