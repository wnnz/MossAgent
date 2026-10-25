namespace MossAgent.Mcp;

/// <summary>
/// Raised when a server answers 404 for a request that carried a previously
/// accepted MCP session id: the session expired or was lost server-side.
/// IOException-derived so existing connection-failure paths still match.
/// </summary>
internal sealed class McpSessionInvalidException() : IOException("MCP HTTP 会话已失效。");
