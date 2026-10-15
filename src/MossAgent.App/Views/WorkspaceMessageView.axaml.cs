using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;

namespace MossAgent.App.Views;

public sealed partial class WorkspaceMessageView : AppUserControl
{
    public WorkspaceMessageView() => InitializeComponent();

    private async void HandleCopyMessage(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { DataContext: ChatMessageViewModel message }) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null) await clipboard.SetTextAsync(message.Content);
    }
}
