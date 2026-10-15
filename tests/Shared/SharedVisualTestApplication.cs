using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using MossAgentApplication = MossAgent.App.App;

namespace MossAgent.Testing;

public class SharedVisualTestApplication : Avalonia.Application
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<SharedVisualTestApplication>()
        .WithInterFont().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    public override void Initialize()
    {
        var visuals = new MossAgentApplication();
        visuals.Initialize();
        var resources = visuals.Resources;
        visuals.Resources = new ResourceDictionary();
        Resources = resources;
        var styles = visuals.Styles.ToArray();
        visuals.Styles.Clear();
        foreach (var style in styles) Styles.Add(style);
    }
}
