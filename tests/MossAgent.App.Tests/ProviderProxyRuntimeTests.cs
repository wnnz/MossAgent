using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App;
using MossAgent.App.ViewModels;
using MossAgent.Application.Models;
using MossAgent.Application.Networking;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProviderProxyRuntimeTests
{
    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Http, false, false)]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Http, true, false)]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Http, false, true)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Http, false, false)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Http, true, false)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Http, false, true)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Http, false, false)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Http, true, false)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Http, false, true)]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Socks5, false, false)]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Socks5, true, false)]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Socks5, false, true)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Socks5, false, false)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Socks5, true, false)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Socks5, false, true)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Socks5, false, false)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Socks5, true, false)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Socks5, false, true)]
    public async Task ConfiguredRoutingCarriesDiscoveryAndStreamingRequests(
        ProviderProtocol protocol, ProxyProtocol proxyProtocol, bool editEndpoint, bool direct)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var original = new TcpListener(IPAddress.Loopback, 0);
        original.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requests = ServeRequestsAsync(listener, protocol, direct ? null : proxyProtocol, timeout.Token);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(timeout.Token);
            var window = services.GetRequiredService<MainWindowViewModel>();
            await window.Providers.RefreshCommand.ExecuteAsync(null);
            await ConfigureProxyAsync(services, editEndpoint ? ((IPEndPoint)original.LocalEndpoint).Port : port, proxyProtocol);
            var baseUrl = direct ? $"http://127.0.0.1:{port}/v1/" : "http://models.example.test/v1/";
            var provider = await ConfigureProviderAsync(services, protocol, baseUrl, direct, timeout.Token);
            if (editEndpoint)
            {
                window.Proxies.OpenEditFormCommand.Execute(Assert.Single(window.Proxies.Items));
                window.Proxies.Port = port;
                await window.Proxies.SaveCommand.ExecuteAsync(null);
            }

            var discovered = await services.GetRequiredService<IModelDiscoveryService>()
                .DiscoverAsync(provider, timeout.Token);
            var modelId = Assert.Single(discovered).Id;
            var model = new ModelProfile(Guid.NewGuid(), provider.Id, modelId, modelId,
                8192, 1024, "off", null, null, false, false, false, true, "{}");
            var request = new ModelRequest(provider, model,
                [new ModelMessage(ModelRole.User, "hello")], []);
            var events = await ReadEventsAsync(services, request, timeout.Token);

            Assert.Contains(events, modelEvent => modelEvent is TextDelta { Text: "hello" });
            Assert.Contains(events, modelEvent => modelEvent is ModelCompleted);
            var prefix = direct || proxyProtocol == ProxyProtocol.Socks5 ? "/v1/" : baseUrl;
            Assert.Equal([
                $"GET {prefix}models HTTP/1.1",
                $"POST {prefix}{ProviderRoutingTestProtocol.StreamPath(protocol)} HTTP/1.1"
            ], await requests);
            Assert.False(original.Pending());
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Http)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Http)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Http)]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Socks5)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Socks5)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Socks5)]
    public async Task DisabledConfiguredProxyRejectsDiscoveryAndStreamingWithoutDirectFallback(
        ProviderProtocol protocol, ProxyProtocol proxyProtocol)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var proxy = new TcpListener(IPAddress.Loopback, 0);
        using var direct = new TcpListener(IPAddress.Loopback, 0);
        proxy.Start();
        direct.Start();
        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(timeout.Token);
            var window = services.GetRequiredService<MainWindowViewModel>();
            await window.Providers.RefreshCommand.ExecuteAsync(null);
            await ConfigureProxyAsync(services, ((IPEndPoint)proxy.LocalEndpoint).Port, proxyProtocol);
            var provider = await ConfigureProviderAsync(services, protocol,
                $"http://127.0.0.1:{((IPEndPoint)direct.LocalEndpoint).Port}/v1/", false, timeout.Token);
            window.Proxies.OpenEditFormCommand.Execute(Assert.Single(window.Proxies.Items));
            window.Proxies.IsEnabled = false;
            await window.Proxies.SaveCommand.ExecuteAsync(null);
            Assert.False(Assert.Single(window.Proxies.Items).IsEnabled);

            await Assert.ThrowsAsync<ProviderProxyUnavailableException>(() =>
                services.GetRequiredService<IModelDiscoveryService>().DiscoverAsync(provider, timeout.Token));
            var model = new ModelProfile(Guid.NewGuid(), provider.Id, "fixture-model", "fixture-model",
                8192, 1024, "off", null, null, false, false, false, true, "{}");
            var request = new ModelRequest(provider, model, [new ModelMessage(ModelRole.User, "hello")], []);
            await Assert.ThrowsAsync<ProviderProxyUnavailableException>(() =>
                ReadEventsAsync(services, request, timeout.Token));
            Assert.False(proxy.Pending());
            Assert.False(direct.Pending());
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<List<ModelEvent>> ReadEventsAsync(
        ServiceProvider services, ModelRequest request, CancellationToken cancellationToken)
    {
        var events = new List<ModelEvent>();
        var provider = services.GetRequiredService<IModelProviderRegistry>().Get(request.Provider.Protocol);
        await foreach (var modelEvent in provider.StreamAsync(request, cancellationToken)) events.Add(modelEvent);
        return events;
    }

    private static async Task ConfigureProxyAsync(ServiceProvider services, int port, ProxyProtocol protocol)
    {
        var viewModel = services.GetRequiredService<ProxySettingsViewModel>();
        await viewModel.RefreshCommand.ExecuteAsync(null);
        viewModel.OpenCreateFormCommand.Execute(null);
        viewModel.Name = "local-proxy";
        viewModel.Host = "127.0.0.1";
        viewModel.Port = port;
        viewModel.Protocol = protocol;
        viewModel.IsDefault = true;
        await viewModel.SaveCommand.ExecuteAsync(null);
    }

    private static async Task<AiProvider> ConfigureProviderAsync(
        ServiceProvider services,
        ProviderProtocol protocol,
        string baseUrl,
        bool direct,
        CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<IConfigurationRepository>();
        var proxyId = Assert.Single(await repository.GetProxiesAsync(cancellationToken)).Id;
        var viewModel = services.GetRequiredService<ProviderSettingsViewModel>();
        viewModel.OpenCreateFormCommand.Execute(null);
        viewModel.Name = "fixture";
        viewModel.Protocol = protocol;
        viewModel.BaseUrl = baseUrl;
        viewModel.ApiKey = "fixture-key";
        viewModel.SelectedProxy = Assert.Single(viewModel.Proxies, item => item.Id == (direct ? null : proxyId));
        await viewModel.SaveCommand.ExecuteAsync(null);
        return Assert.Single(await repository.GetProvidersAsync(cancellationToken));
    }

    private static async Task<string[]> ServeRequestsAsync(
        TcpListener listener,
        ProviderProtocol protocol,
        ProxyProtocol? proxyProtocol,
        CancellationToken cancellationToken)
    {
        string[] bodies = [
            ProviderRoutingTestProtocol.Models(protocol),
            ProviderRoutingTestProtocol.Stream(protocol)
        ];
        var requests = new List<string>();
        foreach (var body in bodies)
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = client.GetStream();
            if (proxyProtocol == ProxyProtocol.Socks5)
                await Socks5TestHandshake.AcceptAsync(stream, "models.example.test", 80, cancellationToken);
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            requests.Add(await reader.ReadLineAsync(cancellationToken) ?? string.Empty);
            while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 }) { }
            var bytes = Encoding.UTF8.GetBytes(body);
            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(bytes, cancellationToken);
        }

        return requests.ToArray();
    }
}
