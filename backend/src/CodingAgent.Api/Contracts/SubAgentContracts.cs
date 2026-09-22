namespace CodingAgent.Api.Contracts.SubAgents;

public record SubAgentDto(int Id, string Name, string Description, string InvocationRule, string SystemPrompt, int ProviderId, string ModelId, string ReasoningEffort, int MaxTurns, string AllowedToolsJson, bool Enabled);
public record CreateSubAgentRequest(string Name, string Description, string InvocationRule, string SystemPrompt, int ProviderId, string ModelId, string ReasoningEffort, int MaxTurns, string AllowedToolsJson, bool Enabled);
public record UpdateSubAgentRequest(string Name, string Description, string InvocationRule, string SystemPrompt, int ProviderId, string ModelId, string ReasoningEffort, int MaxTurns, string AllowedToolsJson, bool Enabled);
