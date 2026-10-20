using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.Tests;

internal sealed class ScopedProbeTool(string name, ToolCapability capabilities, ToolRiskLevel risk = ToolRiskLevel.ReadOnly) : IAgentTool
{
    public int Executions { get; private set; }
    public ToolDescriptor Descriptor { get; } = new(name, "fixture", """{"type":"object","properties":{},"additionalProperties":false}""", risk, capabilities);

    public Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        Executions++;
        return Task.FromResult(ToolResult.Success("fixture"));
    }
}
