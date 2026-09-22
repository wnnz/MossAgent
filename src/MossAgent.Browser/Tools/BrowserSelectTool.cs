using MossAgent.Tools.Abstractions;

namespace MossAgent.Browser.Tools;

public sealed class BrowserSelectTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.select",
        "选择当前页面下拉框中的选项，可使用 value 或标签文本。",
        """{"type":"object","required":["selector","value"],"properties":{"selector":{"type":"string"},"value":{"type":"string"}}}""",
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
        var value = request.Arguments.GetRequiredString("value");
        await session.SelectOptionAsync(selector, value, cancellationToken);
        return ToolResult.Success($"已选择下拉选项：{selector}");
    }
}
