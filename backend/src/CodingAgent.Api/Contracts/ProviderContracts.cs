namespace CodingAgent.Api.Contracts.Providers;

public record ProviderModelDto(int Id, int ProviderId, string ModelId, string DisplayName, bool SupportsTools, bool SupportsReasoning, string DefaultReasoningEffort, int MaxContextTokens, int MaxOutputTokens, bool IsCustom);
public record ProviderDto(int Id, string Name, string Type, string BaseUrl, string ApiKey, int? ProxyId, bool Enabled, List<ProviderModelDto> Models);
public record CreateProviderRequest(string Name, string Type, string BaseUrl, string ApiKey, int? ProxyId, bool Enabled);
public record UpdateProviderRequest(string Name, string Type, string BaseUrl, string ApiKey, int? ProxyId, bool Enabled);
public record CreateModelRequest(string ModelId, string DisplayName, bool SupportsTools, bool SupportsReasoning, string DefaultReasoningEffort, int MaxContextTokens, int MaxOutputTokens);
public record UpdateModelRequest(string ModelId, string DisplayName, bool SupportsTools, bool SupportsReasoning, string DefaultReasoningEffort, int MaxContextTokens, int MaxOutputTokens, bool IsCustom);
public record ModelRefreshResponse(int Added, int Updated, List<ProviderModelDto> Models);
