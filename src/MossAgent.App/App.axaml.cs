using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
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
    private DesktopApplicationInstance? _instance;
    private bool _exiting;

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
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = mainWindow;
            _startup = _services.GetRequiredService<AppStartupService>();
            _automations = _services.GetRequiredService<AutomationScheduler>();
            var workspace = _services.GetRequiredService<WorkspaceViewModel>();
            _closeCoordinator = new WindowCloseCoordinator(mainWindow, workspace.Shutdown,
                () => ShutdownAsync(workspace), desktop);
            mainWindow.Opened += HandleWindowOpened;
            desktop.Exit += HandleExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    internal void AttachInstance(DesktopApplicationInstance instance)
    {
        _instance?.ActivationRequested -= HandleActivationRequested;
        _instance = instance;
        instance.ActivationRequested += HandleActivationRequested;
    }

    private void HandleActivationRequested()
    {
        if (_exiting) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_exiting || _services is null) return;
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            {
                if (!window.IsVisible) window.Show();
                if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                window.Activate();
            }
        });
    }

    private async Task ShutdownAsync(WorkspaceViewModel workspace)
    {
        _exiting = true;
        if (_automations is not null) await _automations.StopAsync();
        await workspace.ShutdownAsync();
        if (_startup is not null) await _startup.DisposeAsync();
        if (_services is not null) await _services.DisposeAsync();
    }

    private async void HandleWindowOpened(object? sender, EventArgs args)
    {
        if (_exiting) return;
        if (sender is MainWindow window)
        {
            window.Opened -= HandleWindowOpened;
            window.WindowState = WindowState.Maximized;
        }
        if (_startup is not null) await _startup.StartAsync();
        if (_exiting || _services is null) return;
        if (_automations is not null && _services.GetRequiredService<WorkspaceViewModel>().IsShuttingDown == false)
            await _automations.StartAsync();
    }

    private void HandleExit(
        object? sender,
        ControlledApplicationLifetimeExitEventArgs args)
    {
        if (_instance is not null) _instance.ActivationRequested -= HandleActivationRequested;
        _instance = null;
        _closeCoordinator?.Dispose();
        _closeCoordinator = null;
        _startup = null;
        _automations = null;
        _services = null;
    }
}
