using MossAgent.Tools.Abstractions;

namespace MossAgent.Browser.Tools;

public sealed class BrowserScrollTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.scroll",
        "滚动整个页面或指定的可滚动元素。",
        """{"type":"object","properties":{"selector":{"type":["string","null"]},"deltaX":{"type":"integer"},"deltaY":{"type":"integer"}}}""",
        ToolRiskLevel.ExternalSideEffect,
        ToolCapability.BrowserWrite);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var session = context.BrowserSession
            ?? throw new InvalidOperationException("当前任务没有绑定浏览器会话。");
        var selector = request.Arguments.GetOptionalString("selector");
        var deltaX = request.Arguments.GetOptionalInt32("deltaX") ?? 0;
        var deltaY = request.Arguments.GetOptionalInt32("deltaY") ?? 600;
        await session.ScrollAsync(selector, deltaX, deltaY, cancellationToken);
        return ToolResult.Success(
            selector is null
                ? $"页面已滚动：({deltaX}, {deltaY})"
                : $"元素已滚动：{selector} ({deltaX}, {deltaY})");
    }
}
