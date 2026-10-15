using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.Tests;

internal sealed class StringSchemaProbeTool(string schemaJson) : IAgentTool
{
    public bool WasExecuted { get; private set; }

    public ToolDescriptor Descriptor { get; } = new("test.string_schema", "String schema fixture",
        schemaJson, ToolRiskLevel.Mutation, ToolCapability.FileWrite);

    public Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WasExecuted = true;
        return Task.FromResult(ToolResult.Success("Validated."));
    }
}
