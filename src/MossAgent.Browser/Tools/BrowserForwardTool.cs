using MossAgent.Tools.Abstractions;

namespace MossAgent.Browser.Tools;

public sealed class BrowserForwardTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.forward",
        "让当前任务绑定的浏览器前进一页。",
        """{"type":"object","properties":{}}""",
        ToolRiskLevel.ExternalSideEffect,
        ToolCapability.Network | ToolCapability.BrowserWrite);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var session = context.BrowserSession
            ?? throw new InvalidOperationException("当前任务没有绑定浏览器会话。");
        await session.GoForwardAsync(cancellationToken);
        return ToolResult.Success($"浏览器已前进到 {session.CurrentUri}");
    }
}
