using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.Platform.Windows.Terminal;

public sealed class TerminalWriteTool(ITerminalSessionManager terminals) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "terminal.write", "向当前任务的交互式终端发送输入。",
        """{"type":"object","required":["input"],"properties":{"input":{"type":"string"}}}""",
        ToolRiskLevel.ExternalSideEffect, ToolCapability.Process);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var input = request.Arguments.GetRequiredString("input");
        await terminals.WriteAsync(context.TaskId, input, cancellationToken);
        return ToolResult.Success($"已向终端写入 {input.Length} 个字符。");
    }
}

