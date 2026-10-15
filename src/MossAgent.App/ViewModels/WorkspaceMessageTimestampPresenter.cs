using Avalonia.Threading;

namespace MossAgent.App.ViewModels;

internal static class WorkspaceMessageTimestampPresenter
{
    public static async Task StampAsync(ChatMessageViewModel message, DateTimeOffset createdAt)
    {
        if (!message.HasContent) return;
        if (Avalonia.Application.Current is null || Dispatcher.UIThread.CheckAccess())
            message.SetCreatedAt(createdAt);
        else
            await Dispatcher.UIThread.InvokeAsync(() => message.SetCreatedAt(createdAt));
    }
}
