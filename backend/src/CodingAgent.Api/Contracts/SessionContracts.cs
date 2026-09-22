namespace CodingAgent.Api.Contracts.Sessions;

public record SessionDto(Guid Id, string Title, int ProviderId, string ModelId, string ReasoningEffort, string? WorkspacePath, string Status, DateTime CreatedAt, DateTime UpdatedAt);
public record CreateSessionRequest(string Title, int ProviderId, string ModelId, string ReasoningEffort, string? WorkspacePath);
public record UpdateSessionRequest(string? Title, int? ProviderId, string? ModelId, string? ReasoningEffort, string? WorkspacePath, string? Status);
public record ChatMessageDto(long Id, Guid SessionId, string Role, string Content, string? ToolCallsJson, string? ToolCallId, string? Name, DateTime CreatedAt);
