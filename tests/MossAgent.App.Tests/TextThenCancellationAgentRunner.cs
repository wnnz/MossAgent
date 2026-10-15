using System.Runtime.CompilerServices;
using MossAgent.Application.Agent;

namespace MossAgent.App.Tests;

internal sealed class TextThenCancellationAgentRunner(Action cancel) : IAgentRunner
{
    public async IAsyncEnumerable<AgentEvent> RunAsync(
        AgentRunRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new AgentTextEvent("已经流出的部分回复。");
        cancel();
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
    }
}
