using System.Collections.ObjectModel;
using Avalonia.Threading;
using MossAgent.Application.Agent;

namespace MossAgent.App.ViewModels;

internal static class WorkspaceAgentEventPresenter
{
    public static async Task<ChatMessageViewModel> PresentAsync(
        AgentEvent agentEvent,
        ChatMessageViewModel assistant,
        ObservableCollection<ChatMessageViewModel> messages,
        Action<string> setActivity,
        Action<AgentEvent> afterPresent)
    {
        if (Avalonia.Application.Current is null || Dispatcher.UIThread.CheckAccess())
            return ApplyEvent(agentEvent, assistant, messages, setActivity, afterPresent);
        return await Dispatcher.UIThread.InvokeAsync(() => ApplyEvent(
            agentEvent, assistant, messages, setActivity, afterPresent));
    }

    internal static ChatMessageViewModel ApplyEvent(
        AgentEvent agentEvent,
        ChatMessageViewModel assistant,
        ObservableCollection<ChatMessageViewModel> messages,
        Action<string> setActivity,
        Action<AgentEvent> afterPresent)
    {
        var nextAssistant = assistant;
        switch (agentEvent)
        {
            case AgentTextEvent text:
                if (!messages.Contains(assistant)) messages.Add(assistant);
                assistant.Append(text.Delta);
                break;
            case AgentToolProgressEvent progress:
                if (!Enum.IsDefined(progress.Phase)) break;
                if (!assistant.HasContent) messages.Remove(assistant);
                WorkspaceToolProgressPresenter.Apply(messages, progress);
                break;
            case AgentToolEvent tool:
                if (!assistant.HasContent) messages.Remove(assistant);
                var completed = ToolConversationPayload.FromEvent(tool);
                var running = WorkspaceToolMessageLookup.FindRunning(messages, tool.CallId);
                if (running is null)
                    messages.Add(ChatMessageViewModel.CreateTool(completed, tool.CallId));
                else
                    running.UpdateTool(completed, false);
                nextAssistant = new ChatMessageViewModel("MossAgent", string.Empty);
                break;
            case AgentSubagentProgressEvent childProgress:
                var current = WorkspaceToolMessageLookup.FindRunning(messages, childProgress.CallId);
                var dialogue = SubagentDialogueProgress.Apply(current?.Tool?.Dialogue, childProgress.Message);
                var inProgress = new ToolConversationPayload(
                    "subagent.run", false, WorkspaceText.Get("SubagentRunning"), null, dialogue);
                if (current is null)
                    messages.Add(ChatMessageViewModel.CreateTool(
                        inProgress, childProgress.CallId, true));
                else
                {
                    var openDialogue = current.Tool?.HasDialogue != true;
                    current.UpdateTool(inProgress, true);
                    if (openDialogue) current.IsToolExpanded = true;
                }
                break;
            case AgentStatusEvent status:
                setActivity(status.Status);
                break;
            case AgentSteeringAppliedEvent input:
                nextAssistant = ApplySteering(input, assistant, messages);
                break;
            case AgentFailureEvent failure:
                var failureHint = WorkspaceFailureText.Get(TaskFailureClassifier.FromCode(failure.Code));
                agentEvent = new AgentFailureEvent(TaskFailureClassifier.SafeCode(failure.Code), failureHint);
                if (!assistant.HasContent) messages.Remove(assistant);
                FinishRunningTools(messages, "run_failed", failureHint);
                setActivity(failureHint);
                break;
            case AgentCancelledEvent:
                if (!assistant.HasContent) messages.Remove(assistant);
                var cancelled = WorkspaceText.Get("RunCancelled");
                FinishRunningTools(messages, "tool_cancelled", cancelled);
                setActivity(cancelled);
                break;
            case AgentCompletedEvent:
                if (!assistant.HasContent) messages.Remove(assistant);
                FinishRunningTools(messages, "tool_incomplete", WorkspaceText.Get("ToolIncomplete"));
                setActivity(WorkspaceText.Get("RunCompleted"));
                break;
        }

        afterPresent(agentEvent);
        return nextAssistant;
    }

    private static ChatMessageViewModel ApplySteering(AgentSteeringAppliedEvent input, ChatMessageViewModel assistant,
        ObservableCollection<ChatMessageViewModel> messages)
    {
        if (!assistant.HasContent) messages.Remove(assistant);
        else if (input.Application.PrecedingAssistant is { } previous) assistant.SetCreatedAt(previous.CreatedAt);
        foreach (var message in input.Application.Messages) messages.Add(WorkspaceConversationMapper.ToChatMessage(message));
        return new ChatMessageViewModel("MossAgent", string.Empty);
    }

    private static void FinishRunningTools(
        IEnumerable<ChatMessageViewModel> messages, string errorCode, string summary)
    {
        foreach (var message in messages)
        {
            if (!message.IsToolRunning || message.Tool is not { } tool) continue;
            message.UpdateTool(tool with
            {
                IsSuccess = false,
                ErrorCode = errorCode,
                Summary = summary
            }, false);
        }
    }
}
