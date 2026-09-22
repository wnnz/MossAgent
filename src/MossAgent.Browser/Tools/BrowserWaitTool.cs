using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser.Tools;

public sealed class BrowserWaitTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.wait",
        "等待页面元素进入指定状态。",
        """{"type":"object","required":["selector"],"properties":{"selector":{"type":"string"},"state":{"type":"string","enum":["visible","hidden","attached","detached"]},"timeoutMs":{"type":"integer","minimum":100,"maximum":60000}}}""",
        ToolRiskLevel.ReadOnly,
        ToolCapability.BrowserRead);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var session = context.BrowserSession
            ?? throw new InvalidOperationException("当前任务没有绑定浏览器会话。");
        var selector = request.Arguments.GetRequiredString("selector");
        var stateValue = request.Arguments.GetOptionalString("state") ?? "visible";
        if (!Enum.TryParse<BrowserElementState>(stateValue, true, out var state))
        {
            return ToolResult.Failure("元素状态无效。", "invalid_state");
        }

        var timeoutMs = Math.Clamp(
            request.Arguments.GetOptionalInt32("timeoutMs") ?? 10_000, 100, 60_000);
        await session.WaitForAsync(
            selector, state, TimeSpan.FromMilliseconds(timeoutMs), cancellationToken);
        return ToolResult.Success($"元素已进入 {state} 状态：{selector}");
    }
}
