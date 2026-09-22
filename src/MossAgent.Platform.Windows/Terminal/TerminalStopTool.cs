using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.Platform.Windows.Terminal;

public sealed class TerminalStopTool(ITerminalSessionManager terminals) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "terminal.stop", "停止当前任务的交互式终端。",
        """{"type":"object","properties":{}}""",
        ToolRiskLevel.Mutation, ToolCapability.Process);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        await terminals.StopAsync(context.TaskId);
        return ToolResult.Success("交互式终端已停止。");
    }
}
