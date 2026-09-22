using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.Application.Persistence;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Mcp;

namespace MossAgent.App;

public sealed partial class App : Avalonia.Application
{
    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        _services = AppComposition.CreateServices();
        _services.GetRequiredService<IDatabaseInitializer>()
            .InitializeAsync().GetAwaiter().GetResult();
        _services.GetRequiredService<IMcpManager>()
            .ReloadAsync().GetAwaiter().GetResult();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainWindowViewModel>()
            };
            desktop.Exit += HandleExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void HandleExit(
        object? sender,
        ControlledApplicationLifetimeExitEventArgs args)
    {
        _services?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _services = null;
    }
}
