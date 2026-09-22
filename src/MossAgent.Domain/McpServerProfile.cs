namespace MossAgent.Domain;

public sealed record McpServerProfile(
    Guid Id,
    string Name,
    McpTransportKind Transport,
    string? Command,
    string ArgumentsJson,
    string? WorkingDirectory,
    string EnvironmentJson,
    Uri? Url,
    string HeadersJson,
    Guid? ProxyId,
    bool IsEnabled);

