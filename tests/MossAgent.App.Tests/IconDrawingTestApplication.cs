using Avalonia;
using Avalonia.Headless;

namespace MossAgent.App.Tests;

public sealed class IconDrawingTestApplication : SharedVisualTestApplication
{
    public new static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<IconDrawingTestApplication>()
        .WithInterFont().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
