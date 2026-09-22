namespace MossAgent.Application.Agent;

public interface IAgentRunner
{
    IAsyncEnumerable<AgentEvent> RunAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken);
}

