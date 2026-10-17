using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class ProviderUiWorkflowFixture : IAsyncDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
    private readonly CancellationTokenSource _timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private ServiceProvider? _services;
    private ProviderUiWorkflowServer? _server;

    private ProviderUiWorkflowFixture() => _timeout.CancelAfter(TimeSpan.FromSeconds(60));

    public MainWindowViewModel Main => _services!.GetRequiredService<MainWindowViewModel>();
    public IConfigurationRepository Configurations => _services!.GetRequiredService<IConfigurationRepository>();
    public IWorkspaceRepository Tasks => _services!.GetRequiredService<IWorkspaceRepository>();
    public ProviderUiWorkflowServer Server => _server!;
    public MainWindow Window { get; private set; } = null!;
    public AiProvider UntouchedProvider { get; private set; } = null!;
    public ModelProfile UntouchedModel { get; private set; } = null!;
    public CancellationToken Token => _timeout.Token;

    public static async Task<ProviderUiWorkflowFixture> CreateAsync(ProviderProtocol protocol, bool dark)
    {
        var fixture = new ProviderUiWorkflowFixture();
        try
        {
            Directory.CreateDirectory(fixture._root);
            fixture._services = AppComposition.CreateServices(fixture._root);
            await fixture._services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
            fixture._server = new ProviderUiWorkflowServer(protocol, fixture.Token);
            fixture.UntouchedProvider = new AiProvider(Guid.NewGuid(), "untouched", ProviderProtocol.OpenAiResponses,
                new Uri("https://untouched.example.test/v1/"), string.Empty, null, false, false, new Dictionary<string, string>());
            fixture.UntouchedModel = new ModelProfile(Guid.NewGuid(), fixture.UntouchedProvider.Id, "untouched-model", "Untouched",
                16000, 1024, "none", null, null, false, false, false, true, "{}");
            await fixture.Configurations.SaveProviderAsync(fixture.UntouchedProvider, fixture.Token);
            await fixture.Configurations.SaveModelAsync(fixture.UntouchedModel, fixture.Token);
            await fixture.Main.Models.RefreshProvidersCommand.ExecuteAsync(null);
            await fixture.Main.Workspace.LoadAsync();
            fixture.Window = new MainWindow
            {
                DataContext = fixture.Main,
                Width = dark ? 526 : 1280,
                Height = dark ? 601 : 820,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            fixture.Window.Show();
            fixture.Main.SelectSettingsTabCommand.Execute("0");
            ProviderUiWorkflowInput.Flush(fixture.Window);
            Assert.Equal(dark ? ThemeVariant.Dark : ThemeVariant.Light, fixture.Window.ActualThemeVariant);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public async Task WaitUntilAsync(Func<bool> predicate)
    {
        while (!predicate())
        {
            ProviderUiWorkflowInput.Flush(Window);
            await Task.Delay(15, Token);
        }
        ProviderUiWorkflowInput.Flush(Window);
    }

    public async Task AssertUntouchedAsync()
    {
        var provider = Assert.Single(await Configurations.GetProvidersAsync(Token), item => item.Id == UntouchedProvider.Id);
        Assert.Equal(UntouchedProvider with { Headers = provider.Headers }, provider);
        Assert.Empty(provider.Headers);
        Assert.Equal(UntouchedModel, Assert.Single(await Configurations.GetModelsAsync(provider.Id, Token)));
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
            if (_server is not null) await _server.DisposeAsync();
            await _timeout.CancelAsync();
            SqliteTestPool.Clear(_root);
            var target = Path.GetFullPath(_root);
            var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MossAgent.Tests"));
            if (!target.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unexpected provider workflow test root.");
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            _timeout.Dispose();
        }
    }
}
