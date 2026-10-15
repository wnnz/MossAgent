using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using MossAgent.App.Controls;
using MossAgent.App.ViewModels;

namespace MossAgent.App.Views;

public sealed partial class MainWindow : AppWindow
{
    public MainWindow()
    {
        InitializeComponent();
        UpdateWindowStateVisuals();
        PropertyChanged += HandleWindowPropertyChanged;
    }

    private void HandleWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == Window.WindowStateProperty)
        {
            ResizeGrips.IsVisible = WindowState == WindowState.Normal;
            UpdateWindowStateVisuals();
        }
    }

    private void UpdateWindowStateVisuals()
    {
        MaximizeButton.IsAlternateIcon = WindowState == WindowState.Maximized;
    }

    private void HandleNavigationToggle(object? sender, RoutedEventArgs args)
    {
        var navigation = DataContext is MainWindowViewModel { IsSettingsOpen: true }
            ? SettingsView.Navigation : WorkspaceNavigation;
        navigation.ToggleNavigationCommand.Execute(null);
    }

    private void HandleResizeGripPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (WindowState != WindowState.Normal
            || !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || sender is not Control { Tag: string edgeName }
            || !Enum.TryParse<WindowEdge>(edgeName, true, out var edge))
        {
            args.Handled = true;
            return;
        }

        BeginResizeDrag(edge, args);
    }

    private void HandleTitleBarPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || IsButtonSource(args.Source))
        {
            return;
        }

        if (args.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }

        BeginMoveDrag(args);
    }

    private static bool IsButtonSource(object? source)
    {
        for (var control = source as Control; control is not null; control = control.Parent as Control)
        {
            if (control is Button)
            {
                return true;
            }
        }

        return false;
    }
}
