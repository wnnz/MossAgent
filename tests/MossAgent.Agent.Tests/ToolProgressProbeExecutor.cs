using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent.Tests;

internal sealed class ToolProgressProbeExecutor(
    Func<ToolRequest, ToolExecutionContext, CancellationToken, Task<ToolResult>> execute) : IToolExecutor
{
    public Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken) =>
        execute(request, context, cancellationToken);
}
