using Avalonia.Controls;

namespace MossAgent.App.Services;

public static class WindowChromeCommands
{
    public static void Minimize(Window? window)
    {
        if (window is not null) window.WindowState = WindowState.Minimized;
    }

    public static void ToggleMaximize(Window? window)
    {
        if (window is not null)
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    public static void Close(Window? window) => window?.Close();
}
