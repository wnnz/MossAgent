using MossAgent.Tools.Abstractions;

namespace MossAgent.Browser.Tools;

public sealed class BrowserTypeTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.type",
        "向当前页面中的输入元素填写文本。",
        """{"type":"object","required":["selector","text"],"properties":{"selector":{"type":"string"},"text":{"type":"string"}}}""",
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
        var text = request.Arguments.GetRequiredString("text");
        await session.TypeAsync(selector, text, cancellationToken);
        return ToolResult.Success($"已向元素填写 {text.Length} 个字符：{selector}");
    }
}

