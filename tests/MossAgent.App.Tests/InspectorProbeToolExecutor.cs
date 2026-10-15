using MossAgent.Tools.Abstractions;

namespace MossAgent.App.Tests;

internal sealed class InspectorProbeToolExecutor : IToolExecutor
{
    public Func<ToolRequest, ToolExecutionContext, CancellationToken, Task<ToolResult>> Execute { get; set; } =
        (_, _, _) => Task.FromResult(ToolResult.Success("synthetic", string.Empty));
    public Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken) =>
        Execute(request, context, cancellationToken);
}
