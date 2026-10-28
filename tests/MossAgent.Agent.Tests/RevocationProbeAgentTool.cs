using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent.Tests;

/// <summary>
/// Workspace-bound probe tool that signals admission, then blocks inside execution until its
/// caller token is cancelled — letting a test land an authorization revocation while the
/// call is genuinely in-flight.
/// </summary>
internal sealed class RevocationProbeAgentTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "test.wait", "等待直到取消",
        """{"type":"object","properties":{}}""",
        ToolRiskLevel.ReadOnly, ToolCapability.FileRead);

    public int Calls { get; private set; }
    public bool Cancelled { get; private set; }
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        Calls++;
        Started.TrySetResult();
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return ToolResult.Success("已完成");
        }
        finally
        {
            Cancelled = cancellationToken.IsCancellationRequested;
        }
    }
}
