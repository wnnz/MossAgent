namespace CodingAgent.Api.Contracts.Mcp;

public record McpServerDto(int Id, string Name, string Transport, string? Command, string? ArgsJson, string? EnvJson, string? Url, string? HeadersJson, bool Enabled, bool AutoConnect);
public record CreateMcpServerRequest(string Name, string Transport, string? Command, string? ArgsJson, string? EnvJson, string? Url, string? HeadersJson, bool Enabled, bool AutoConnect);
public record UpdateMcpServerRequest(string Name, string Transport, string? Command, string? ArgsJson, string? EnvJson, string? Url, string? HeadersJson, bool Enabled, bool AutoConnect);
public record McpToolDto(int Id, int ServerId, string Name, string Description, string SchemaJson);
public record McpToolCallRequest(object? Arguments);
public record McpToolCallResponse(bool Success, string Result);
