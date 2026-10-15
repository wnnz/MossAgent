using System.Runtime.CompilerServices;
using MossAgent.Application.Agent;

namespace MossAgent.App.Tests;

internal sealed class HistoryCaptureAgentRunner : IAgentRunner
{
    public AgentRunRequest? Request { get; private set; }

    public async IAsyncEnumerable<AgentEvent> RunAsync(AgentRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Request = request;
        await Task.Yield();
        yield return new AgentCompletedEvent();
    }
}
