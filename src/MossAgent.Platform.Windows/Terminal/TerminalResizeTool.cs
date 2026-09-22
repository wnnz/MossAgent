using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.Platform.Windows.Terminal;

public sealed class TerminalResizeTool(ITerminalSessionManager terminals) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "terminal.resize", "调整当前任务终端的行列尺寸。",
        """{"type":"object","required":["columns","rows"],"properties":{"columns":{"type":"integer","minimum":20,"maximum":500},"rows":{"type":"integer","minimum":5,"maximum":200}}}""",
        ToolRiskLevel.ReadOnly, ToolCapability.Process);

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var columns = Math.Clamp(request.Arguments.GetOptionalInt32("columns") ?? 120, 20, 500);
        var rows = Math.Clamp(request.Arguments.GetOptionalInt32("rows") ?? 30, 5, 200);
        terminals.Resize(context.TaskId, columns, rows);
        return Task.FromResult(ToolResult.Success($"终端尺寸已调整为 {columns}x{rows}。"));
    }
}

