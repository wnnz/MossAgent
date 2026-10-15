using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.Tests;

internal sealed class ApprovalExecutionProbeTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new("approval.probe", "approval fixture", "{}",
        ToolRiskLevel.Mutation, ToolCapability.FileWrite);

    public int ExecutionCount { get; private set; }

    public Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken token)
    {
        ExecutionCount++;
        return Task.FromResult(ToolResult.Success("fixture executed"));
    }
}
