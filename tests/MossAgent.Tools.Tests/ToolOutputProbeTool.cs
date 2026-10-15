using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.Tests;

internal sealed class ToolOutputProbeTool(ToolResult output, Exception? failure = null) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "test.output_probe", "测试输出限制。", "{\"type\":\"object\"}",
        ToolRiskLevel.ReadOnly, ToolCapability.FileRead);

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken) => failure is null
            ? Task.FromResult(output)
            : Task.FromException<ToolResult>(failure);
}
