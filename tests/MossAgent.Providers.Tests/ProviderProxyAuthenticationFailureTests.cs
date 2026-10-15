using System.Net;
using System.Text;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.Streaming;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ProviderProxyAuthenticationFailureTests
{
    private const string Username = "proxy-fixture-user";
    private const string Password = "proxy-fixture-secret";
    private const string SafeMessage = "代理认证失败，请检查所选代理的用户名和密码。";

    public static IEnumerable<object[]> Responses()
    {
        foreach (var protocol in Enum.GetValues<ProviderProtocol>())
            foreach (var body in new[] { "credentials", "empty", "invalid", "oversized", "unfinished" })
                foreach (var declaredLength in new[] { false, true })
                    yield return [protocol, body, declaredLength];
    }

    [Theory]
    [MemberData(nameof(Responses))]
    public async Task Streaming407NeverReadsResponseBody(
        ProviderProtocol protocol, string body, bool declaredLength)
    {
        using var timeout = Timeout();
        using var handler = Handler(body, declaredLength);

        var events = await ProviderStreamingTestFixture.CollectAsync(protocol, handler, timeout.Token);

        Assert.True(events.Count == 1 && events[0] is ModelFailed);
        var failure = (ModelFailed)events[0];
        Assert.Equal("proxy_authentication_failed", failure.Code);
        AssertNoCredentials(failure.Message);
        Assert.True(failure.Message == $"HTTP 407: {SafeMessage}");
        AssertUnreadAndDisposed(handler);
    }

    [Theory]
    [MemberData(nameof(Responses))]
    public async Task Discovery407NeverReadsResponseBody(
        ProviderProtocol protocol, string body, bool declaredLength)
    {
        using var timeout = Timeout();
        using var handler = Handler(body, declaredLength);
        var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));

        var exception = await Record.ExceptionAsync(() => service.DiscoverAsync(
            ProviderStreamingTestFixture.CreateRequest(protocol).Provider, timeout.Token));

        Assert.True(exception is HttpRequestException);
        var failure = (HttpRequestException)exception;
        Assert.Equal(HttpStatusCode.ProxyAuthenticationRequired, failure.StatusCode);
        Assert.Null(failure.InnerException);
        AssertNoCredentials(failure.Message);
        Assert.True(failure.Message.Contains(SafeMessage, StringComparison.Ordinal));
        AssertUnreadAndDisposed(handler);
    }

    [Fact]
    public async Task ErrorReaderHonorsCancellationBeforeMapping407()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        using var stream = new ChunkedProviderReadStream([], blockAtEnd: true);
        using var content = new StreamContent(stream);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProviderHttpErrorReader.ReadAsync(
            content, HttpStatusCode.ProxyAuthenticationRequired,
            ProviderStreamingTestFixture.CreateRequest(ProviderProtocol.OpenAiResponses).Provider, cancellation.Token));

        Assert.Equal(0, stream.BytesRead);
        Assert.False(stream.BlockedRead.Task.IsCompleted);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, false)]
    [InlineData(ProviderProtocol.OpenAiCompatible, false)]
    [InlineData(ProviderProtocol.AnthropicMessages, false)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, false)]
    [InlineData(ProviderProtocol.OpenAiResponses, true)]
    [InlineData(ProviderProtocol.OpenAiCompatible, true)]
    [InlineData(ProviderProtocol.AnthropicMessages, true)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, true)]
    public async Task CancellationAfterHeadersDoesNotRead407AndDisposesResponse(
        ProviderProtocol protocol, bool streaming)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var handler = new ProviderByteResponseHandler([], HttpStatusCode.ProxyAuthenticationRequired,
            blockAtEnd: true, onResponse: cancellation.Cancel);

        if (streaming)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                ProviderStreamingTestFixture.CollectAsync(protocol, handler, cancellation.Token));
        }
        else
        {
            var service = new ModelDiscoveryService(new StaticProviderHttpClientFactory(handler));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DiscoverAsync(
                ProviderStreamingTestFixture.CreateRequest(protocol).Provider, cancellation.Token));
        }

        AssertUnreadAndDisposed(handler);
    }

    private static ProviderByteResponseHandler Handler(string body, bool declaredLength)
    {
        var bytes = body switch
        {
            "empty" => [],
            "invalid" => new byte[] { 0xC3, 0x28 },
            "oversized" => new byte[2 * 1024 * 1024 + 1],
            _ => Encoding.UTF8.GetBytes($"{Username}:{Password}")
        };
        return new ProviderByteResponseHandler(bytes, HttpStatusCode.ProxyAuthenticationRequired,
            declaredLength, blockAtEnd: body == "unfinished");
    }

    private static void AssertNoCredentials(string value)
    {
        Assert.False(value.Contains(Username, StringComparison.Ordinal));
        Assert.False(value.Contains(Password, StringComparison.Ordinal));
    }

    private static void AssertUnreadAndDisposed(ProviderByteResponseHandler handler)
    {
        Assert.NotNull(handler.ResponseStream);
        Assert.Equal(0, handler.ResponseStream.BytesRead);
        Assert.False(handler.ResponseStream.BlockedRead.Task.IsCompleted);
        Assert.True(handler.ResponseStream.IsDisposed);
    }

    private static CancellationTokenSource Timeout()
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        return timeout;
    }
}
