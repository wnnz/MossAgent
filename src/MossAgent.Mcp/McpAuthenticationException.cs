using System.Net;

namespace MossAgent.Mcp;

/// <summary>
/// Raised when the server rejects a request with 401/403. Carries the
/// WWW-Authenticate scheme when the server sent one so callers can point the
/// user at a concrete credential fix instead of a generic failure.
/// </summary>
internal sealed class McpAuthenticationException(HttpStatusCode statusCode, string? challengeScheme)
    : HttpRequestException($"MCP HTTP 服务认证或权限失败（{(int)statusCode}）。", null, statusCode)
{
    public new HttpStatusCode StatusCode { get; } = statusCode;
    public string? ChallengeScheme { get; } = challengeScheme;
}
