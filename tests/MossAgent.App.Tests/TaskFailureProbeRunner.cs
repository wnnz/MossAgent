using System.Runtime.CompilerServices;
using MossAgent.Application.Agent;

namespace MossAgent.App.Tests;

internal sealed class TaskFailureProbeRunner(string code, Exception? failure = null, Action? cancel = null) : IAgentRunner
{
    public async IAsyncEnumerable<AgentEvent> RunAsync(
        AgentRunRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new AgentTextEvent("partial response");
        await Task.Yield();
        cancel?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        if (failure is not null) throw failure;
        yield return new AgentFailureEvent(code, "Bearer synthetic-secret; endpoint=http://credentials.example.test/");
    }
}
