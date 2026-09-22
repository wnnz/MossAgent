using System.Text.Json;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Browser.Tools;

public sealed class BrowserTabsTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.tabs",
        "列出当前任务浏览器会话中的标签页。",
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
        var tabs = await session.ListTabsAsync(cancellationToken);
        return ToolResult.Success($"当前有 {tabs.Count} 个浏览器标签页。", JsonSerializer.Serialize(tabs));
    }
}
