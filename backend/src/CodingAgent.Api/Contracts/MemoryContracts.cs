namespace CodingAgent.Api.Contracts.Memories;

public record MemoryDto(int Id, string Scope, string Title, string Content, string? Tags, DateTime UpdatedAt);
public record CreateMemoryRequest(string Scope, string Title, string Content, string? Tags);
public record UpdateMemoryRequest(string Scope, string Title, string Content, string? Tags);
