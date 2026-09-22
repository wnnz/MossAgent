using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceRunExecutor(
    IWorkspaceRepository workspaces,
    IAgentRunner agent,
    WorkspaceTaskFactory taskFactory)
{
    public async Task<AgentTask> ExecuteAsync(
        WorkspaceRunState state,
        ToolExecutionContext context,
        Func<AgentEvent, Task> present,
        CancellationToken cancellationToken)
    {
        var history = await workspaces.GetMessagesAsync(state.Task.Id, cancellationToken);
        var request = new AgentRunRequest(
            state.Provider,
            state.Model,
            history.Select(WorkspaceConversationMapper.ToModelMessage).ToArray(),
            context);
        var failed = false;
        await foreach (var agentEvent in agent.RunAsync(request, cancellationToken))
        {
            failed |= agentEvent is AgentFailureEvent;
            await present(agentEvent);
        }

        return await taskFactory.CompleteAsync(
            state.Task,
            state.Assistant.Content,
            failed ? AgentTaskStatus.Failed : AgentTaskStatus.Completed,
            cancellationToken);
    }
}
