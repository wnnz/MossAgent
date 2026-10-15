using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent.Tests;

internal sealed class FileChangeSubagentGateway(IToolCatalog catalog, IToolExecutor executor) : ISubagentToolGateway
{
    public IReadOnlyList<ToolDescriptor> Descriptors => catalog.Descriptors.ToArray();

    public Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(request, context with { SubagentTools = null, SubagentProgress = null }, cancellationToken);
}
