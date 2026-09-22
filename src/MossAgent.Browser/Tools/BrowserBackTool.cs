using MossAgent.Tools.Abstractions;

namespace MossAgent.Browser.Tools;

public sealed class BrowserBackTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.back",
        "让当前任务绑定的浏览器后退一页。",
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
        await session.GoBackAsync(cancellationToken);
        return ToolResult.Success($"浏览器已后退到 {session.CurrentUri}");
    }
}
