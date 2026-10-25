namespace MossAgent.Application.Persistence;

/// <summary>
/// Raised when an MCP configuration write targets a stale revision. The caller
/// must reload the current profile and retry instead of overwriting blindly.
/// </summary>
public sealed class McpConfigurationConflictException()
    : Exception("The MCP configuration was modified by another operation.");
