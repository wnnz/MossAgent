using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.Platform.Windows.Terminal;

public sealed class TerminalStartTool(ITerminalSessionManager terminals) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "terminal.start", "为当前任务启动交互式 PowerShell ConPTY 终端。",
        """{"type":"object","properties":{}}""",
        ToolRiskLevel.ExternalSideEffect, ToolCapability.Process);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        await terminals.StartAsync(context.TaskId, context.WorkingDirectory, cancellationToken);
        return ToolResult.Success("交互式终端已启动。");
    }
}

