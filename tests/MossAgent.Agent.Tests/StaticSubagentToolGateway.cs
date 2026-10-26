using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent.Tests;

/// <summary>Returns a fixed result for every delegated child tool call.</summary>
internal sealed class StaticSubagentToolGateway(ToolResult result) : ISubagentToolGateway
{
    public IReadOnlyList<ToolDescriptor> Descriptors { get; } =
        [new ToolDescriptor("test.read", "fixture tool", """{"type":"object"}""",
            ToolRiskLevel.ReadOnly, ToolCapability.None)];

    public Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context,
        CancellationToken cancellationToken) => Task.FromResult(result);
}
