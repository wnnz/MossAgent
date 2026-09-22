using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.Tests;

internal sealed class InvalidSchemaTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "test.invalid_schema",
        "测试无效架构。",
        "{not-json",
        ToolRiskLevel.ReadOnly,
        ToolCapability.FileRead);

    public bool WasExecuted { get; private set; }

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        WasExecuted = true;
        return Task.FromResult(ToolResult.Success("unexpected"));
    }
}
