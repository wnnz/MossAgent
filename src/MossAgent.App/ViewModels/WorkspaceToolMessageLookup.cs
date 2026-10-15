namespace MossAgent.App.ViewModels;

internal static class WorkspaceToolMessageLookup
{
    public static ChatMessageViewModel? FindRunning(IReadOnlyList<ChatMessageViewModel> messages, string callId)
    {
        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index];
            if (message.IsToolRunning && message.ToolCallId == callId) return message;
        }
        return null;
    }

    public static ChatMessageViewModel? FindTool(IReadOnlyList<ChatMessageViewModel> messages, string callId, string toolName)
    {
        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index];
            if (message.ToolCallId == callId
                && message.Tool?.ToolName.Equals(toolName, StringComparison.OrdinalIgnoreCase) == true) return message;
        }
        return null;
    }
}
