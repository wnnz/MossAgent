namespace MossAgent.Mcp;

/// <summary>
/// Raised when a tool call targets a client generation that is draining for
/// reload. The call was never dispatched; callers must not retry against the
/// stale generation implicitly.
/// </summary>
internal sealed class McpReconnectingException()
    : Exception("The MCP server is reconnecting; the call was not dispatched.");
