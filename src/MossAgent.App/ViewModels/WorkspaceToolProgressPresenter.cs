using System.Collections.ObjectModel;
using MossAgent.Application.Agent;

namespace MossAgent.App.ViewModels;

internal static class WorkspaceToolProgressPresenter
{
    public static void Apply(ObservableCollection<ChatMessageViewModel> messages, AgentToolProgressEvent progress)
    {
        var current = WorkspaceToolMessageLookup.FindTool(messages, progress.CallId, progress.ToolName);
        if (current is { IsToolRunning: false }) return;
        var summary = WorkspaceToolProgressText.Summary(progress.ToolName, progress.Phase);
        var payload = current?.Tool is { } tool ? tool with { Summary = summary }
            : new ToolConversationPayload(progress.ToolName, false, summary, null);
        if (current is null)
        {
            current = ChatMessageViewModel.CreateTool(payload, progress.CallId, true);
            messages.Add(current);
        }
        else current.UpdateTool(payload, true);
        current.UpdateToolPhase(progress.Phase);
    }
}
