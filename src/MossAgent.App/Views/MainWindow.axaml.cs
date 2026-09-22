using Avalonia.Controls;
using Avalonia.Input;

namespace MossAgent.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

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
