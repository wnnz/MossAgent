using System.Collections.ObjectModel;
using Avalonia.Threading;
using MossAgent.Application.Agent;

namespace MossAgent.App.ViewModels;

internal static class WorkspaceAgentEventPresenter
{
    public static async Task PresentAsync(
        AgentEvent agentEvent,
        ChatMessageViewModel assistant,
        ObservableCollection<ChatMessageViewModel> messages,
        Action<string> setActivity)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (agentEvent)
            {
                case AgentTextEvent text:
                    assistant.Append(text.Delta);
                    break;
                case AgentToolEvent tool:
                    messages.Add(new ChatMessageViewModel(
                        "工具", $"{tool.ToolName}: {tool.Result.Summary}"));
                    break;
                case AgentStatusEvent status:
                    setActivity(status.Status);
                    break;
                case AgentFailureEvent failure:
                    setActivity($"失败：{failure.Message}");
                    break;
                case AgentCompletedEvent:
                    setActivity("任务已完成。");
                    break;
            }
        });
    }
}

