using Avalonia;
using Avalonia.Headless;

namespace MossAgent.App.Visual.Tests;

public sealed class SharedPixelTestApplication : SharedVisualTestApplication
{
    public new static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<SharedPixelTestApplication>()
        .WithInterFont().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia();
}
