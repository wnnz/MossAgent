using Microsoft.Extensions.DependencyInjection;
using MossAgent.App;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProxySettingsViewModelTests
{
    [Fact]
    public async Task SaveCommand_PersistsMultipleProxiesAndUpdatesOnlySelectedProxy()
    {
        var root = CreateRoot();
        try
        {
            await using var services = await CreateServicesAsync(root);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            var viewModel = services.GetRequiredService<ProxySettingsViewModel>();
            await viewModel.RefreshCommand.ExecuteAsync(null);
            Assert.True(viewModel.HasNoItems);

            await AddProxyAsync(viewModel, "socks-proxy", 1080, ProxyProtocol.Socks5);
            await AddProxyAsync(viewModel, "http-proxy", 8080, ProxyProtocol.Http);

            Assert.Equal(2, viewModel.Items.Count);
            Assert.False(viewModel.HasNoItems);
            var first = Assert.Single(viewModel.Items, item => item.Name == "socks-proxy");
            var second = Assert.Single(viewModel.Items, item => item.Name == "http-proxy");
            viewModel.SelectedItem = first;
            viewModel.Host = "proxy.example.test";
            viewModel.Port = 1081;
            await viewModel.SaveCommand.ExecuteAsync(null);

            var saved = await repository.GetProxiesAsync(CancellationToken.None);
            Assert.Equal(2, saved.Count);
            var updated = Assert.Single(saved, item => item.Id == first.Id);
            Assert.Equal("proxy.example.test", updated.Host);
            Assert.Equal(1081, updated.Port);
            Assert.Equal(ProxyProtocol.Socks5, updated.Protocol);
            Assert.Contains(saved, item => item.Id == second.Id && item.Port == 8080);

            viewModel.SelectedItem = viewModel.Items.Single(item => item.Id == second.Id);
            await viewModel.DeleteCommand.ExecuteAsync(null);

            Assert.Single(await repository.GetProxiesAsync(CancellationToken.None));
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveCommand_RejectsInvalidPortWithoutPersistingProxy()
    {
        var root = CreateRoot();
        try
        {
            await using var services = await CreateServicesAsync(root);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            var viewModel = services.GetRequiredService<ProxySettingsViewModel>();
            await viewModel.RefreshCommand.ExecuteAsync(null);
            viewModel.OpenCreateFormCommand.Execute(null);
            viewModel.Name = "invalid-proxy";
            viewModel.Host = "127.0.0.1";
            viewModel.Port = 65536;

            await viewModel.SaveCommand.ExecuteAsync(null);

            Assert.Empty(await repository.GetProxiesAsync(CancellationToken.None));
            Assert.True(viewModel.IsFormOpen);
            Assert.Contains("有效", viewModel.Status);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveCommand_RejectsMalformedHostWithoutPersistingProxy()
    {
        var root = CreateRoot();
        try
        {
            await using var services = await CreateServicesAsync(root);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            var viewModel = services.GetRequiredService<ProxySettingsViewModel>();
            await viewModel.RefreshCommand.ExecuteAsync(null);
            viewModel.OpenCreateFormCommand.Execute(null);
            viewModel.Name = "invalid-host";
            viewModel.Host = "proxy.example/path";
            viewModel.Port = 8080;

            await viewModel.SaveCommand.ExecuteAsync(null);

            Assert.Empty(await repository.GetProxiesAsync(CancellationToken.None));
            Assert.True(viewModel.IsFormOpen);
            Assert.Contains("主机名", viewModel.Status);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveCommand_PersistsProxyCredentialsAndResetsProtocolForNewProxy()
    {
        var root = CreateRoot();
        try
        {
            await using var services = await CreateServicesAsync(root);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            var viewModel = services.GetRequiredService<ProxySettingsViewModel>();
            await viewModel.RefreshCommand.ExecuteAsync(null);
            viewModel.OpenCreateFormCommand.Execute(null);
            viewModel.Name = "authenticated-proxy";
            viewModel.Host = "proxy.example.test";
            viewModel.Port = 8080;
            viewModel.Protocol = ProxyProtocol.Socks5;
            viewModel.Username = "proxy-user";
            viewModel.Password = "proxy-password";

            await viewModel.SaveCommand.ExecuteAsync(null);

            var saved = Assert.Single(await repository.GetProxiesAsync(CancellationToken.None));
            Assert.Equal(ProxyProtocol.Socks5, saved.Protocol);
            Assert.Equal("proxy-user", saved.Username);
            Assert.Equal("proxy-password", saved.Password);

            viewModel.OpenCreateFormCommand.Execute(null);
            Assert.Equal(ProxyProtocol.Http, viewModel.Protocol);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<ServiceProvider> CreateServicesAsync(string root)
    {
        var services = AppComposition.CreateServices(root);
        await services.GetRequiredService<IDatabaseInitializer>()
            .InitializeAsync(CancellationToken.None);
        return services;
    }

    private static async Task AddProxyAsync(
        ProxySettingsViewModel viewModel,
        string name,
        int port,
        ProxyProtocol protocol)
    {
        viewModel.OpenCreateFormCommand.Execute(null);
        viewModel.Name = name;
        viewModel.Host = "127.0.0.1";
        viewModel.Port = port;
        viewModel.Protocol = protocol;
        await viewModel.SaveCommand.ExecuteAsync(null);
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
