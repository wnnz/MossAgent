using MossAgent.Tools.Abstractions;

namespace MossAgent.Browser.Tools;

public sealed class BrowserNewTabTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.new_tab",
        "新建并切换到浏览器标签页，可选导航到 HTTP(S) URL。",
        """{"type":"object","properties":{"url":{"type":["string","null"],"format":"uri"}}}""",
        ToolRiskLevel.ExternalSideEffect,
        ToolCapability.Network | ToolCapability.BrowserWrite);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var session = context.BrowserSession
            ?? throw new InvalidOperationException("当前任务没有绑定浏览器会话。");
        var value = request.Arguments.GetOptionalString("url");
        if (!BrowserNavigateTool.TryCreateWebUri(value, optional: true, out var uri))
        {
            return ToolResult.Failure("URL 必须是有效的 HTTP(S) 地址。", "invalid_url");
        }

        var id = await session.NewTabAsync(uri, cancellationToken);
        return ToolResult.Success($"已新建浏览器标签页：{id}", id);
    }
}
