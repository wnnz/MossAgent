using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.Tests;

internal sealed class LateCompletionProbeTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new("late.probe", "late completion fixture", "{}",
        ToolRiskLevel.Mutation, ToolCapability.FileWrite);
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<ToolResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationToken Token { get; private set; }
    public int ExecutionCount { get; private set; }

    public Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken token)
    {
        ExecutionCount++;
        Token = token;
        Started.TrySetResult();
        return Completion.Task;
    }
}
