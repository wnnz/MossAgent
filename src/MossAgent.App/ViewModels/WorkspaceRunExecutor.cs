using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Application.Attachments;
using MossAgent.Application.SourceControl;
using MossAgent.Application.Conversations;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceRunExecutor(
    IWorkspaceRepository workspaces,
    IAgentRunner agent,
    WorkspaceTaskFactory taskFactory,
    IConversationAttachmentStore? attachments = null,
    SourceReviewChatAccess? reviewChats = null,
    IConversationSteeringRepository? steering = null)
{
    public async Task<AgentTask> ExecuteAsync(
        WorkspaceRunState state,
        ToolExecutionContext context,
        Func<AgentEvent, Task> present,
        CancellationToken cancellationToken)
    {
        var review = reviewChats is null ? null : await reviewChats.ResolveAsync(state.Task, cancellationToken);
        var sourceReads = review?.Mode == SourceReviewChatMode.ReadOnlySource;
        if (review is not null) context = context with
        { SourceScope = review.Scope, SourceReviewRevision = sourceReads ? review.Revision : null,
            ToolsEnabled = sourceReads, AllowedToolNames = sourceReads ? null : new HashSet<string>(), WorkingDirectory = string.Empty,
            Timeout = TimeSpan.FromSeconds(30),
            AuthorizedRoots = [], WorkspaceGuard = null, BrowserSession = null, SubagentTools = null, ConversationContext = null };
        await context.ValidateWorkspaceAsync(cancellationToken);
        var history = await workspaces.GetMessagesAsync(state.Task.Id, cancellationToken);
        var messages = await new WorkspaceConversationHistoryBuilder(attachments)
            .BuildAsync(state.Task, state.Model, history, cancellationToken);
        if (history.LastOrDefault(message => message.Role == MessageRole.User)?.Attachments is not { Count: > 0 })
            WorkspaceAttachmentContextAppender.AppendToLastUserMessage(messages, state.AttachmentContext);
        if (review is not null)
        {
            await reviewChats!.ValidateAsync(review, cancellationToken);
        }
        var steeringAccess = new WorkspaceSteeringAccess(workspaces, state.Task, state.Project, context, reviewChats, review);
        state.Steering = steering is null ? null : await ConversationSteeringSession.OpenAsync(
            steering, state.Task, cancellationToken, cancellationToken,
            steeringAccess.ValidateAsync, new ConversationSteeringAuthorization(state.Project, review),
            steeringAccess.ValidateEmptyBoundaryAsync, attachments, state.Model)
            ?? throw new ConversationSteeringException("SteeringRunUnavailable");
        try
        {
            var request = new AgentRunRequest(
                state.Provider,
                state.Model,
                messages,
                context,
                AgentInstructions: review is null ? state.AgentInstructions : sourceReads ? SourceReviewContextBuilder.ReadOnlyInstructions : SourceReviewContextBuilder.Instructions,
                PinnedUserContext: review?.Context, Steering: state.Steering,
                RunId: state.Steering?.RunId ?? Guid.NewGuid());
            var failed = false;
            TaskFailureReason? failureReason = null;
            await foreach (var agentEvent in agent.RunAsync(request, cancellationToken))
            {
                if (review is not null) await reviewChats!.ValidateAsync(review, cancellationToken);
                if (agentEvent is AgentFailureEvent failure)
                {
                    if (!failed) failureReason = TaskFailureClassifier.FromCode(failure.Code);
                    failed = true;
                }
                if (agentEvent is AgentToolEvent tool)
                {
                    await PersistToolEventAsync(state, tool, CancellationToken.None);
                }
                if (agentEvent is AgentSteeringAppliedEvent) state.MarkAssistantPersisted();
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
        finally
        {
            if (state.Steering is not null) await state.Steering.StopAsync(CancellationToken.None);
        }
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
