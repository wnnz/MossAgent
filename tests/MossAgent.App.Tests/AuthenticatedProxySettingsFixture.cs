using Microsoft.Extensions.DependencyInjection;
using MossAgent.App;
using MossAgent.App.ViewModels;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.App.Tests;

internal sealed class AuthenticatedProxySettingsFixture : IAsyncDisposable
{
    private AuthenticatedProxySettingsFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Services = AppComposition.CreateServices(Root);
    }

    public string Root { get; }
    public ServiceProvider Services { get; }
    public MainWindowViewModel Window => Services.GetRequiredService<MainWindowViewModel>();

    public static async Task<AuthenticatedProxySettingsFixture> CreateAsync(ProxyProtocol protocol,
        int port, bool missing, bool wrong, CancellationToken token)
    {
        var fixture = new AuthenticatedProxySettingsFixture();
        try
        {
            await fixture.Services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(token);
            await fixture.Window.Providers.RefreshCommand.ExecuteAsync(null);
            var editor = fixture.Window.Proxies;
            await editor.RefreshCommand.ExecuteAsync(null);
            editor.OpenCreateFormCommand.Execute(null);
            editor.Name = "authenticated-loopback";
            editor.Protocol = protocol;
            editor.Host = "127.0.0.1";
            editor.Port = port;
            editor.Username = missing ? string.Empty : AuthenticatedProxyTestServer.Username;
            editor.Password = missing ? string.Empty : wrong ? "wrong-fixture-secret" : AuthenticatedProxyTestServer.Password;
            editor.IsDefault = true;
            await editor.SaveCommand.ExecuteAsync(null);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public async Task<AiProvider> SaveProviderAsync(ProviderProtocol protocol, string baseUrl,
        bool direct, CancellationToken token)
    {
        var editor = Window.Providers;
        editor.OpenCreateFormCommand.Execute(null);
        editor.Name = "authenticated-provider-fixture";
        editor.Protocol = protocol;
        editor.BaseUrl = baseUrl;
        editor.ApiKey = "provider-fixture-key";
        var proxyId = Window.Proxies.Items.Single().Id;
        editor.SelectedProxy = editor.Proxies.Single(item => item.Id == (direct ? null : proxyId));
        await editor.SaveCommand.ExecuteAsync(null);
        return (await Services.GetRequiredService<IConfigurationRepository>().GetProvidersAsync(token)).Single();
    }

    public async Task<List<ModelEvent>> StreamAsync(AiProvider provider, CancellationToken token)
    {
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "fixture-model", "Fixture",
            8192, 1024, "off", null, null, false, false, false, true, "{}");
        var request = new ModelRequest(provider, model, [new ModelMessage(ModelRole.User, "hello")], []);
        var events = new List<ModelEvent>();
        await foreach (var item in Services.GetRequiredService<IModelProviderRegistry>()
                           .Get(provider.Protocol).StreamAsync(request, token)) events.Add(item);
        return events;
    }

    public async Task ChangePasswordAsync(bool wrong, CancellationToken token)
    {
        var editor = Window.Proxies;
        editor.OpenEditFormCommand.Execute(editor.Items.Single());
        editor.Password = wrong ? "wrong-fixture-secret" : AuthenticatedProxyTestServer.Password;
        await editor.SaveCommand.ExecuteAsync(null).WaitAsync(token);
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        SqliteTestPool.Clear(Root);
        Directory.Delete(Root, recursive: true);
    }
}
