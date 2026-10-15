using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.Application.Persistence;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.App.Services;
using MossAgent.Platform.Windows.Windowing;
using MossAgent.Application.Automations;

namespace MossAgent.App;

public sealed partial class App : Avalonia.Application
{
    private ServiceProvider? _services;
    private AppStartupService? _startup;
    private WindowCloseCoordinator? _closeCoordinator;
    private AutomationScheduler? _automations;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        _services = AppComposition.CreateServices(
            Environment.GetEnvironmentVariable("MOSSAGENT_DATA_DIR"));
        _services.GetRequiredService<IDatabaseInitializer>()
            .InitializeAsync().GetAwaiter().GetResult();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainWindowViewModel>()
            };
            var windowCornerService = _services.GetRequiredService<WindowCornerService>();
            mainWindow.Opened += (_, _) =>
            {
                if (mainWindow.TryGetPlatformHandle() is { } platformHandle)
                {
                    windowCornerService.ApplyRoundedCorners(platformHandle.Handle);
                }
            };
            desktop.MainWindow = mainWindow;
            _startup = _services.GetRequiredService<AppStartupService>();
            _automations = _services.GetRequiredService<AutomationScheduler>();
            var workspace = _services.GetRequiredService<WorkspaceViewModel>();
            _closeCoordinator = new WindowCloseCoordinator(mainWindow, workspace.Shutdown, async () =>
            {
                if (_automations is not null) await _automations.StopAsync();
                await workspace.ShutdownAsync();
                if (_startup is not null) await _startup.DisposeAsync();
            }, desktop);
            mainWindow.Opened += HandleWindowOpened;
            desktop.Exit += HandleExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void HandleWindowOpened(object? sender, EventArgs args)
    {
        if (sender is MainWindow window)
        {
            window.Opened -= HandleWindowOpened;
            window.WindowState = WindowState.Maximized;
        }
        if (_startup is not null) await _startup.StartAsync();
        if (_automations is not null && _services?.GetRequiredService<WorkspaceViewModel>().IsShuttingDown == false)
            await _automations.StartAsync();
    }

    private void HandleExit(
        object? sender,
        ControlledApplicationLifetimeExitEventArgs args)
    {
        _closeCoordinator?.Dispose();
        _closeCoordinator = null;
        _startup?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _startup = null;
        _automations = null;
        _services?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _services = null;
    }
}
