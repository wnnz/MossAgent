using MossAgent.Application.Models;

namespace MossAgent.App.ViewModels;

internal static class WorkspaceAttachmentContextAppender
{
    public static void AppendToLastUserMessage(
        IList<ModelMessage> messages,
        string? attachmentContext)
    {
        if (string.IsNullOrWhiteSpace(attachmentContext)) return;

        for (var index = messages.Count - 1; index >= 0; index--)
        {
            if (messages[index].Role != ModelRole.User) continue;
            messages[index] = messages[index] with
            {
                Content = messages[index].Content + attachmentContext
            };
            return;
        }
    }
}
