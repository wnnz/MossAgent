using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

namespace MossAgent.App.Tests;

internal static class HeadlessRenderedFrame
{
    public static void Flush(Window window)
    {
        // Layout coordinates and rendered hit-testing must describe the same frame.
        // The first frame can enqueue further layout work; publish that work too.
        for (var frame = 0; frame < 2; frame++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }
}
