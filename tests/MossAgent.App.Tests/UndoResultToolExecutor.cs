using MossAgent.Tools.Abstractions;

namespace MossAgent.App.Tests;

internal sealed class UndoResultToolExecutor(Func<ToolRequest, ToolExecutionContext, CancellationToken, Task<ToolResult>> execute) : IToolExecutor
{
    public int Calls { get; private set; }
    public Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        Calls++;
        return execute(request, context, cancellationToken);
    }
}
