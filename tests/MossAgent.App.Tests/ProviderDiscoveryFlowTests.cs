using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProviderDiscoveryFlowTests
{
    [Fact]
    public async Task SettingsToDiscovery_UsesSelectedProxyAndPersistsDiscoveredModel()
    {
        var root = CreateRoot();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var request = ServeModelsAsync(listener, timeout.Token);
        try
        {
            await using var services = await CreateServicesAsync(root, timeout.Token);
            var (provider, proxy) = await SaveSettingsAsync(services, port, timeout.Token);
            var models = services.GetRequiredService<ModelSettingsViewModel>();
            await models.RefreshProvidersCommand.ExecuteAsync(null);

            await models.FetchModelsCommand.ExecuteAsync(null).WaitAsync(timeout.Token);

            var (requestLine, authorization) = await request;
            Assert.Equal("GET http://models.example.test/v1/models HTTP/1.1", requestLine);
            Assert.Equal("Bearer fixture-key", authorization);
            Assert.Equal(proxy.Id, provider.ProxyId);
            var saved = Assert.Single(await services.GetRequiredService<IConfigurationRepository>()
                .GetModelsAsync(provider.Id, timeout.Token));
            Assert.Equal("fixture-model", saved.ModelId);
            Assert.Equal("Fixture Model", saved.DisplayName);
            Assert.Equal(saved.Id, Assert.Single(models.Models).Id);
        }
        finally
        {
            timeout.Cancel();
            try { await request; } catch (OperationCanceledException) { }
            SqliteTestPool.Clear(root);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SettingsToDiscovery_RejectsProxyDisabledAfterSavingProvider()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await using var services = await CreateServicesAsync(root, cancellationToken);
            var (provider, proxy) = await SaveSettingsAsync(services, 65535, cancellationToken);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            await repository.SaveProxyAsync(proxy with { IsEnabled = false }, cancellationToken);
            var models = services.GetRequiredService<ModelSettingsViewModel>();
            await models.RefreshProvidersCommand.ExecuteAsync(null);

            await models.FetchModelsCommand.ExecuteAsync(null);

            Assert.Contains("代理不存在或已禁用", models.Status);
            Assert.Empty(await repository.GetModelsAsync(provider.Id, cancellationToken));
        }
        finally
        {
            SqliteTestPool.Clear(root);
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SettingsToDiscovery_ExplicitDirectBypassesDefaultProxy()
    {
        var root = CreateRoot();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var target = new TcpListener(IPAddress.Loopback, 0);
        using var proxy = new TcpListener(IPAddress.Loopback, 0);
        target.Start();
        proxy.Start();
        var targetPort = ((IPEndPoint)target.LocalEndpoint).Port;
        var proxyPort = ((IPEndPoint)proxy.LocalEndpoint).Port;
        var targetRequest = ServeModelsAsync(target, timeout.Token);
        var proxyRequest = proxy.AcceptTcpClientAsync(timeout.Token).AsTask();
        try
        {
            await using var services = await CreateServicesAsync(root, timeout.Token);
            var (provider, _) = await SaveSettingsAsync(services, proxyPort, timeout.Token,
                $"http://127.0.0.1:{targetPort}/v1/", useProxy: false);
            var models = services.GetRequiredService<ModelSettingsViewModel>();
            await models.RefreshProvidersCommand.ExecuteAsync(null);

            await models.FetchModelsCommand.ExecuteAsync(null).WaitAsync(timeout.Token);

            Assert.Null(provider.ProxyId);
            Assert.Equal("GET /v1/models HTTP/1.1", (await targetRequest).RequestLine);
            Assert.False(proxyRequest.IsCompleted);
            Assert.Single(await services.GetRequiredService<IConfigurationRepository>()
                .GetModelsAsync(provider.Id, timeout.Token));
        }
        finally
        {
            timeout.Cancel();
            try { await targetRequest; } catch (OperationCanceledException) { }
            try { using var unused = await proxyRequest; } catch (OperationCanceledException) { }
            SqliteTestPool.Clear(root);
            DeleteRoot(root);
        }
    }

    private static async Task<(AiProvider Provider, ProxyProfile Proxy)> SaveSettingsAsync(
        ServiceProvider services,
        int proxyPort,
        CancellationToken cancellationToken,
        string? baseUrl = null,
        bool useProxy = true)
    {
        var repository = services.GetRequiredService<IConfigurationRepository>();
        var proxies = services.GetRequiredService<ProxySettingsViewModel>();
        await proxies.RefreshCommand.ExecuteAsync(null);
        proxies.OpenCreateFormCommand.Execute(null);
        proxies.Name = "fixture-proxy";
        proxies.Host = "127.0.0.1";
        proxies.Port = proxyPort;
        proxies.IsDefault = true;
        await proxies.SaveCommand.ExecuteAsync(null);
        var proxy = Assert.Single(await repository.GetProxiesAsync(cancellationToken));

        var providers = services.GetRequiredService<ProviderSettingsViewModel>();
        await providers.RefreshCommand.ExecuteAsync(null);
        providers.OpenCreateFormCommand.Execute(null);
        providers.Name = "fixture-provider";
        providers.BaseUrl = baseUrl ?? "http://models.example.test/v1/";
        providers.ApiKey = "fixture-key";
        if (useProxy)
            Assert.Equal(proxy.Id, providers.SelectedProxy?.Id);
        else
            providers.SelectedProxy = Assert.Single(providers.Proxies, item => item.Id is null);
        await providers.SaveCommand.ExecuteAsync(null);
        return (Assert.Single(await repository.GetProvidersAsync(cancellationToken)), proxy);
    }

    private static async Task<(string RequestLine, string Authorization)> ServeModelsAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
        var authorization = string.Empty;
        while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 } line)
        {
            if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                authorization = line["Authorization:".Length..].Trim();
        }

        const string body = "{\"data\":[{\"id\":\"fixture-model\",\"display_name\":\"Fixture Model\"}]}";
        var payload = Encoding.UTF8.GetBytes(body);
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(headers, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        return (requestLine, authorization);
    }

    private static async Task<ServiceProvider> CreateServicesAsync(string root, CancellationToken cancellationToken)
    {
        var services = AppComposition.CreateServices(root);
        await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellationToken);
        return services;
    }

    private static string CreateRoot()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "MossAgent.Tests");
        var root = Path.Combine(basePath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        var basePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MossAgent.Tests"));
        var target = Path.GetFullPath(root);
        if (!target.StartsWith(basePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unexpected test directory.");
        Directory.Delete(target, recursive: true);
    }
}
