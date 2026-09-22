using MossAgent.Tools.Abstractions;

namespace MossAgent.Browser.Tools;

public sealed class BrowserSwitchTabTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.switch_tab",
        "切换当前任务浏览器会话的活动标签页。",
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
        await session.SwitchTabAsync(tabId, cancellationToken);
        return ToolResult.Success($"已切换浏览器标签页：{tabId}");
    }
}
