using System.Runtime.CompilerServices;
using MossAgent.Application.Agent;

namespace MossAgent.App.Tests;

internal sealed class RetryProbeAgentRunner(params AgentEvent[][] script) : IAgentRunner
{
    private readonly Queue<AgentEvent[]> _script = new(script);

    public List<AgentRunRequest> Requests { get; } = [];

    public async IAsyncEnumerable<AgentEvent> RunAsync(AgentRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Requests.Add(request);
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        var events = _script.Count > 0 ? _script.Dequeue() : [new AgentCompletedEvent()];
        foreach (var agentEvent in events) yield return agentEvent;
    }
}
