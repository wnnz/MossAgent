using MossAgent.Tools.Abstractions;

namespace MossAgent.Browser.Tools;

public sealed class BrowserCloseTabTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.close_tab",
        "关闭当前任务浏览器会话中的指定标签页。",
        """{"type":"object","required":["tabId"],"properties":{"tabId":{"type":"string"}}}""",
        ToolRiskLevel.ExternalSideEffect,
        ToolCapability.BrowserWrite);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var session = context.BrowserSession
            ?? throw new InvalidOperationException("当前任务没有绑定浏览器会话。");
        var tabId = request.Arguments.GetRequiredString("tabId");
        await session.CloseTabAsync(tabId, cancellationToken);
        return ToolResult.Success($"已关闭浏览器标签页：{tabId}");
    }
}
