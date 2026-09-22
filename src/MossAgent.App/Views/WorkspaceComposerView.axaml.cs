using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MossAgent.App.Views;

public sealed partial class WorkspaceComposerView : UserControl
{
    public WorkspaceComposerView() => InitializeComponent();

    private void HandleApprovalPolicySelected(object? sender, RoutedEventArgs args) =>
        ApprovalPolicyButton.Flyout?.Hide();
}
