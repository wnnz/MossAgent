using MossAgent.Tools.Abstractions;

namespace MossAgent.Browser.Tools;

public sealed class BrowserNavigateTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.navigate",
        "让当前任务绑定的浏览器导航到指定 URL。",
        """{"type":"object","required":["url"],"properties":{"url":{"type":"string","format":"uri"}}}""",
        ToolRiskLevel.ExternalSideEffect,
        ToolCapability.Network | ToolCapability.BrowserWrite);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var session = context.BrowserSession
            ?? throw new InvalidOperationException("当前任务没有绑定浏览器会话。");
        var value = request.Arguments.GetRequiredString("url");
        if (!TryCreateWebUri(value, optional: false, out var uri))
        {
            return ToolResult.Failure("URL 必须是有效的 HTTP(S) 地址。", "invalid_url");
        }

        await session.NavigateAsync(uri!, cancellationToken);
        return ToolResult.Success($"浏览器已导航到 {uri}");
    }

    internal static bool TryCreateWebUri(string? value, bool optional, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return optional;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out uri)
            && uri.Scheme is "http" or "https";
    }
}
