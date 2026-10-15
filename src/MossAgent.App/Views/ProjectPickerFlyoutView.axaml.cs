using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MossAgent.App.ViewModels;

using MossAgent.App.Controls;

namespace MossAgent.App.Views;

public partial class ProjectPickerFlyoutView : AppUserControl
{
    public ProjectPickerFlyoutView() => InitializeComponent();

    private void HandleCreateProjectClick(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not WorkspaceViewModel workspace) return;

        Dispatcher.UIThread.Post(
            () => workspace.Catalog.ProjectEditor.OpenCommand.Execute(null),
            DispatcherPriority.SystemIdle);
    }
}
