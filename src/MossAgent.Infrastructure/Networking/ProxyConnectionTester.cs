using System.Diagnostics;
using System.Net;
using MossAgent.Application.Networking;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Networking;

public sealed class ProxyConnectionTester : IProxyConnectionTester
{
    private static readonly Uri DefaultProbeUri =
        new("https://connectivitycheck.gstatic.com/generate_204");
    private readonly Uri _probeUri;

    public ProxyConnectionTester()
        : this(DefaultProbeUri)
    {
    }

    public ProxyConnectionTester(Uri probeUri) => _probeUri = probeUri;

    public async Task<ProxyConnectionTestResult> TestAsync(
        ProxyProfile proxy,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var handler = CreateHandler(proxy);
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(12)
            };
            using var request = new HttpRequestMessage(HttpMethod.Get, _probeUri);
            request.Headers.UserAgent.ParseAdd("MossAgent/1.0 ProxyTest");
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            stopwatch.Stop();
            var success = (int)response.StatusCode is >= 200 and < 400;
            return new ProxyConnectionTestResult(
                success,
                stopwatch.Elapsed,
                success
                    ? $"代理可用，探测状态 {(int)response.StatusCode}。"
                    : $"代理已连接，但探测返回 {(int)response.StatusCode}。");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            stopwatch.Stop();
            return new ProxyConnectionTestResult(
                false, stopwatch.Elapsed, $"代理测试失败：{exception.Message}");
        }
    }

    private static SocketsHttpHandler CreateHandler(ProxyProfile proxy)
    {
        var webProxy = new WebProxy(proxy.ToUri());
        if (!string.IsNullOrWhiteSpace(proxy.Username))
        {
            webProxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password);
        }

        return new SocketsHttpHandler
        {
            UseProxy = true,
            Proxy = webProxy,
            ConnectTimeout = TimeSpan.FromSeconds(6),
            AllowAutoRedirect = false
        };
    }
}
