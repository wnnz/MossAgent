using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.Tests;

internal sealed class BrowserExceptionProbeTool(Exception exception) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.probe",
        "browser probe fixture",
        """{"type":"object","properties":{},"additionalProperties":false}""",
        ToolRiskLevel.ReadOnly,
        ToolCapability.BrowserRead);

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken) =>
        Task.FromException<ToolResult>(exception);
}
