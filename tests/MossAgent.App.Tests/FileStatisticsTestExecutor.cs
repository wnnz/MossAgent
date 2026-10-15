using System.Collections.Concurrent;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.Tests;

internal sealed class FileStatisticsTestExecutor : IToolExecutor
{
    public ConcurrentQueue<ToolExecutionContext> Contexts { get; } = [];
    public Func<ToolRequest, ToolExecutionContext, CancellationToken, Task<ToolResult>> Handler { get; set; } =
        static (_, _, _) => Task.FromResult(new ToolResult(true, "statistics", "{\"Added\":3,\"Removed\":2}"));

    public Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        Contexts.Enqueue(context);
        return Handler(request, context, cancellationToken);
    }
}
