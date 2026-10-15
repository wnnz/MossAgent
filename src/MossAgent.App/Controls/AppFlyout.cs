using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace MossAgent.App.Controls;

public sealed class AppFlyout : Flyout
{
    private Control? _dismissHandlerRoot;

    public static readonly StyledProperty<bool> DismissOnButtonClickProperty =
        AvaloniaProperty.Register<AppFlyout, bool>(nameof(DismissOnButtonClick));

    public AppFlyout()
    {
        FlyoutPresenterClasses.Add("app-flyout");
        Opened += (_, _) => AttachDismissHandler();
        Closed += (_, _) => DetachDismissHandler();
    }

    public bool DismissOnButtonClick
    {
        get => GetValue(DismissOnButtonClickProperty);
        set => SetValue(DismissOnButtonClickProperty, value);
    }

    private void AttachDismissHandler()
    {
        if (!DismissOnButtonClick || Content is not Control root) return;
        _dismissHandlerRoot = root;
        root.AddHandler(Button.ClickEvent, HandleButtonClick, RoutingStrategies.Bubble);
    }

    private void DetachDismissHandler()
    {
        _dismissHandlerRoot?.RemoveHandler(Button.ClickEvent, HandleButtonClick);
        _dismissHandlerRoot = null;
    }

    private void HandleButtonClick(object? sender, RoutedEventArgs args) =>
        Dispatcher.UIThread.Post(Hide, DispatcherPriority.Background);
}
