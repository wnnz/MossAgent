using MossAgent.Tools.Abstractions;

namespace MossAgent.Browser.Tools;

public sealed class BrowserSnapshotTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.snapshot",
        "读取当前页面的可访问性快照。",
        """{"type":"object","properties":{}}""",
        ToolRiskLevel.ReadOnly,
        ToolCapability.BrowserRead);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var session = context.BrowserSession
            ?? throw new InvalidOperationException("当前任务没有绑定浏览器会话。");
        var snapshot = await session.GetSnapshotAsync(cancellationToken);
        return ToolResult.Success($"已读取页面快照：{session.CurrentUri}", snapshot);
    }
}

