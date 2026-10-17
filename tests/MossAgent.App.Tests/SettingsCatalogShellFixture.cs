using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class SettingsCatalogShellFixture : IAsyncDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
    private readonly CancellationTokenSource _timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private ServiceProvider? _services;

    public MainWindowViewModel Main => _services!.GetRequiredService<MainWindowViewModel>();
    public IConfigurationRepository Configurations => _services!.GetRequiredService<IConfigurationRepository>();
    public IMcpConfigurationRepository Servers => _services!.GetRequiredService<IMcpConfigurationRepository>();
    public MainWindow Window { get; private set; } = null!;
    public CancellationToken Token => _timeout.Token;

    public static async Task<SettingsCatalogShellFixture> CreateAsync(string page, bool dark, int width)
    {
        var fixture = new SettingsCatalogShellFixture();
        fixture._timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            Directory.CreateDirectory(fixture._root);
            fixture._services = AppComposition.CreateServices(fixture._root);
            await fixture._services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
            for (var index = 0; index < 30; index++)
            {
                await fixture.Configurations.SaveProxyAsync(new ProxyProfile(Guid.NewGuid(), $"Proxy {index:00}",
                    ProxyProtocol.Http, "127.0.0.1", 8080 + index, null, null, index == 0, true), fixture.Token);
                await fixture.Servers.SaveAsync(McpSettingsReloadFixture.Server($"Server {index:00}"), fixture.Token);
            }
            await fixture.Main.Proxies.RefreshCommand.ExecuteAsync(null);
            await fixture.Main.Mcp.ReloadCommand.ExecuteAsync(null);
            await fixture.Main.Workspace.LoadAsync();
            fixture.Window = new MainWindow
            {
                DataContext = fixture.Main,
                Width = width,
                Height = 601,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            fixture.Window.Show();
            fixture.Main.SelectSettingsTabCommand.Execute(page == "proxy" ? "1" : "3");
            ProviderUiWorkflowInput.Flush(fixture.Window);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public void Save(string name)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        ProviderUiWorkflowInput.Flush(Window);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var bitmap = Window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        bitmap.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
    }

    public async ValueTask DisposeAsync()
    {
        Window?.Close();
        try
        {
            if (_services is not null) await _services.DisposeAsync();
        }
        finally
        {
            await _timeout.CancelAsync();
            SqliteTestPool.Clear(_root);
            var target = Path.GetFullPath(_root);
            var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MossAgent.Tests"));
            if (!target.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unexpected settings catalog test root.");
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            _timeout.Dispose();
        }
    }
}
