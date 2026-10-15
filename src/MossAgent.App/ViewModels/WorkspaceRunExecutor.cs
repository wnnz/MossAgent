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
        if (history.Any(message => message.TaskId != state.Task.Id))
            throw new InvalidOperationException("Conversation history belongs to another task.");
        var messages = history.Select(WorkspaceConversationMapper.ToModelMessage).ToList();
        WorkspaceAttachmentContextAppender.AppendToLastUserMessage(
            messages, state.AttachmentContext);
        var request = new AgentRunRequest(
            state.Provider,
            state.Model,
            messages,
            context,
            AgentInstructions: state.AgentInstructions);
        var failed = false;
        TaskFailureReason? failureReason = null;
        await foreach (var agentEvent in agent.RunAsync(request, cancellationToken))
        {
            if (agentEvent is AgentFailureEvent failure)
            {
                if (!failed) failureReason = TaskFailureClassifier.FromCode(failure.Code);
                failed = true;
            }
            if (agentEvent is AgentToolEvent tool)
            {
                await PersistToolEventAsync(state, tool, CancellationToken.None);
            }
            await present(agentEvent);
        }

        var completed = await taskFactory.CompleteAsync(
            state.Task,
            state.PendingAssistantContent,
            failed ? AgentTaskStatus.Failed : AgentTaskStatus.Completed,
            cancellationToken, failureReason);
        state.MarkAssistantPersisted();
        return completed;
    }

    private async Task PersistToolEventAsync(
        WorkspaceRunState state,
        AgentToolEvent tool,
        CancellationToken cancellationToken)
    {
        var messages = new List<ConversationMessage>();
        if (!string.IsNullOrWhiteSpace(state.PendingAssistantContent))
            messages.Add(new ConversationMessage(Guid.NewGuid(), state.Task.Id, MessageRole.Assistant,
                state.PendingAssistantContent, DateTimeOffset.UtcNow));
        messages.Add(
            new ConversationMessage(
                Guid.NewGuid(), state.Task.Id, MessageRole.Tool,
                WorkspaceToolMessageSerializer.Serialize(tool),
                DateTimeOffset.UtcNow, tool.CallId));
        await workspaces.SaveTaskWithMessagesAsync(state.Task, messages, cancellationToken);
        state.MarkAssistantPersisted();
    }
}
