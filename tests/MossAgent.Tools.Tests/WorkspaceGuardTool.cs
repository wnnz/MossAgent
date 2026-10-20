using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.Tests;

internal sealed class WorkspaceGuardTool : IAgentTool
{
    public int Executions { get; private set; }
    public ToolDescriptor Descriptor { get; } = new("fixture.workspace", "fixture", """{"type":"object","properties":{},"additionalProperties":false}""",
        ToolRiskLevel.Mutation, ToolCapability.FileWrite);

    public Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        Executions++;
        return Task.FromResult(ToolResult.Success("executed"));
    }
}
