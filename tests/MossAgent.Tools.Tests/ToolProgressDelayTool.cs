using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.Tests;

internal sealed class ToolProgressDelayTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new("progress.delay", "timeout fixture", "{}",
        ToolRiskLevel.ReadOnly, ToolCapability.FileRead);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken token)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
        return ToolResult.Success("must not complete");
    }
}
