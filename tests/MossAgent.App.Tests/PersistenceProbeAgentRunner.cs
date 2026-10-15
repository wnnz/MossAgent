using System.Runtime.CompilerServices;
using MossAgent.Application.Agent;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.Tests;

internal sealed class PersistenceProbeAgentRunner(bool emitTool) : IAgentRunner
{
    public async IAsyncEnumerable<AgentEvent> RunAsync(
        AgentRunRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return new AgentTextEvent("streamed response");
        if (emitTool) yield return new AgentToolEvent("files.read", "fixture-call", ToolResult.Success("read", "synthetic data"));
        await Task.Yield();
        yield return new AgentCompletedEvent();
    }
}
