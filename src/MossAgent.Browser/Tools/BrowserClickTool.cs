using MossAgent.Tools.Abstractions;

namespace MossAgent.Browser.Tools;

public sealed class BrowserClickTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.click",
        "点击当前页面中的元素。",
        """{"type":"object","required":["selector"],"properties":{"selector":{"type":"string"}}}""",
        ToolRiskLevel.ExternalSideEffect,
        ToolCapability.BrowserWrite);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var session = context.BrowserSession
            ?? throw new InvalidOperationException("当前任务没有绑定浏览器会话。");
        var selector = request.Arguments.GetRequiredString("selector");
        await session.ClickAsync(selector, cancellationToken);
        return ToolResult.Success($"已点击元素：{selector}");
    }
}

