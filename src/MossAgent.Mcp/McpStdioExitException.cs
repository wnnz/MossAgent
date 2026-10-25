namespace MossAgent.Mcp;

/// <summary>
/// Raised when the stdio server process exits while requests or gate waiters
/// are still blocked. IOException-derived so existing connection-failure
/// paths still match; carries the exit code when it could be observed.
/// </summary>
internal sealed class McpStdioExitException(int? exitCode) : IOException("MCP server 进程已退出。")
{
    public int? ExitCode { get; } = exitCode;
}
