using Avalonia;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Platform.Windows.Windowing;

namespace MossAgent.App;

internal static class Program
{
    internal static string ResolveDataRoot(string? root) =>
        new AppDataPaths(string.IsNullOrWhiteSpace(root) ? null : root).Root;

    [STAThread]
    public static int Main(string[] args)
    {
        using var instance = DesktopApplicationInstance.Create(
            ResolveDataRoot(Environment.GetEnvironmentVariable("MOSSAGENT_DATA_DIR")));
        if (!instance.IsPrimary)
        {
            instance.SignalActivation();
            return 0;
        }
        return BuildAvaloniaApp()
            .AfterSetup(builder => ((App)builder.Instance!).AttachInstance(instance))
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
