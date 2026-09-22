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
        var messages = history.Where(message => message.Role != MessageRole.Tool)
            .Select(WorkspaceConversationMapper.ToModelMessage).ToList();
        WorkspaceAttachmentContextAppender.AppendToLastUserMessage(
            messages, state.AttachmentContext);
        var request = new AgentRunRequest(
            state.Provider,
            state.Model,
            messages,
            context);
        var failed = false;
        await foreach (var agentEvent in agent.RunAsync(request, cancellationToken))
        {
            failed |= agentEvent is AgentFailureEvent;
            if (agentEvent is AgentToolEvent tool)
            {
                await PersistToolEventAsync(state.Task.Id, tool, cancellationToken);
            }
            await present(agentEvent);
        }

        return await taskFactory.CompleteAsync(
            state.Task,
            state.Assistant.Content,
            failed ? AgentTaskStatus.Failed : AgentTaskStatus.Completed,
            cancellationToken);
    }

    private async Task PersistToolEventAsync(
        Guid taskId,
        AgentToolEvent tool,
        CancellationToken cancellationToken)
    {
        await workspaces.AppendMessageAsync(
            new ConversationMessage(
                Guid.NewGuid(), taskId, MessageRole.Tool,
                WorkspaceToolMessageSerializer.Serialize(tool),
                DateTimeOffset.UtcNow, tool.CallId),
            cancellationToken);
    }
}
