using System.Net.Security;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;
using static MossAgent.Providers.Tests.ProviderStreamingTestFixture;

namespace MossAgent.Providers.Tests;

public sealed class ProviderTlsStreamTests
{
    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public async Task FixedLengthHttps_DeliversPartialTextBeforeReleaseAndCompletes(ProviderProtocol protocol)
    {
        using var timeout = CreateTimeout();
        await using var server = CreateServer(protocol, timeout.Token);
        using var handler = server.CreateHandler();
        var request = CreateRequest(protocol);
        await using var events = CreateProvider(protocol, handler).StreamAsync(
            request with { Provider = request.Provider with { BaseUri = server.BaseUri } },
            timeout.Token).GetAsyncEnumerator(timeout.Token);

        Assert.True(await events.MoveNextAsync());

        Assert.Equal("hello", Assert.IsType<TextDelta>(events.Current).Text);
        await server.PrefixWritten.Task.WaitAsync(timeout.Token);
        Assert.False(server.TailReleased);
        Assert.False(server.Completion.IsCompleted);
        server.ReleaseTail();
        var remaining = new List<ModelEvent>();
        while (await events.MoveNextAsync()) remaining.Add(events.Current);
        Assert.Equal("world", string.Concat(remaining.OfType<TextDelta>().Select(delta => delta.Text)));
        Assert.DoesNotContain(remaining, item => item is ModelFailed);
        await server.Disconnected.Task.WaitAsync(timeout.Token);
        await server.Completion.WaitAsync(timeout.Token);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public async Task FixedLengthHttps_CancellationDisconnectsWithoutReleasingTheTail(ProviderProtocol protocol)
    {
        using var timeout = CreateTimeout();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        await using var server = CreateServer(protocol, timeout.Token);
        using var handler = server.CreateHandler();
        var request = CreateRequest(protocol);
        await using var events = CreateProvider(protocol, handler).StreamAsync(
            request with { Provider = request.Provider with { BaseUri = server.BaseUri } }, cancellation.Token)
            .GetAsyncEnumerator(cancellation.Token);
        Assert.True(await events.MoveNextAsync());
        Assert.Equal("hello", Assert.IsType<TextDelta>(events.Current).Text);
        var pending = events.MoveNextAsync().AsTask();
        Assert.False(pending.IsCompleted);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await server.Disconnected.Task.WaitAsync(timeout.Token);
        await server.Completion.WaitAsync(timeout.Token);
        Assert.False(server.TailReleased);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public async Task FixedLengthHttps_EarlyDisposalDisconnectsWithoutReleasingTheTail(ProviderProtocol protocol)
    {
        using var timeout = CreateTimeout();
        await using var server = CreateServer(protocol, timeout.Token);
        using var handler = server.CreateHandler();
        var request = CreateRequest(protocol);
        await using var events = CreateProvider(protocol, handler).StreamAsync(
            request with { Provider = request.Provider with { BaseUri = server.BaseUri } }, timeout.Token)
            .GetAsyncEnumerator(timeout.Token);
        Assert.True(await events.MoveNextAsync());
        Assert.Equal("hello", Assert.IsType<TextDelta>(events.Current).Text);

        await events.DisposeAsync();

        await server.Disconnected.Task.WaitAsync(timeout.Token);
        await server.Completion.WaitAsync(timeout.Token);
        Assert.False(server.TailReleased);
    }

    [Fact]
    public async Task PrivateCertificatePin_RejectsDifferentCertificatesAndHostnameErrors()
    {
        using var timeout = CreateTimeout();
        await using var server = CreateServer(ProviderProtocol.OpenAiResponses, timeout.Token);
        await using var other = CreateServer(ProviderProtocol.OpenAiResponses, timeout.Token);
        using var handler = server.CreateHandler();
        var validate = Assert.IsType<RemoteCertificateValidationCallback>(handler.SslOptions.RemoteCertificateValidationCallback);

        Assert.True(validate(handler, server.Certificate, null, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(validate(handler, other.Certificate, null, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(validate(handler, server.Certificate, null, SslPolicyErrors.RemoteCertificateNameMismatch));
        Assert.False(validate(handler, null, null, SslPolicyErrors.RemoteCertificateNotAvailable));
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public async Task WrongCertificate_IsRejectedBeforeAnyProviderFrame(ProviderProtocol protocol)
    {
        using var timeout = CreateTimeout();
        await using var server = new ProviderTlsStreamServer("data: private-fixture\n\n", string.Empty,
            timeout.Token, expectCertificateRejection: true);
        await using var other = CreateServer(protocol, timeout.Token);
        using var handler = server.CreateHandler(other.Certificate);
        var request = CreateRequest(protocol);
        await using var events = CreateProvider(protocol, handler).StreamAsync(
            request with { Provider = request.Provider with { BaseUri = server.BaseUri } }, timeout.Token)
            .GetAsyncEnumerator(timeout.Token);

        await Assert.ThrowsAsync<HttpRequestException>(() => events.MoveNextAsync().AsTask());

        await server.CertificateRejected.Task.WaitAsync(timeout.Token);
        await server.Completion.WaitAsync(timeout.Token);
        Assert.False(server.PrefixWritten.Task.IsCompleted);
    }

    private static CancellationTokenSource CreateTimeout()
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        return timeout;
    }

    private static ProviderTlsStreamServer CreateServer(ProviderProtocol protocol, CancellationToken token)
    {
        var prefix = protocol switch
        {
            ProviderProtocol.OpenAiResponses => """{"type":"response.output_text.delta","delta":"hello"}""",
            ProviderProtocol.OpenAiCompatible => """{"choices":[{"index":0,"delta":{"content":"hello"}}]}""",
            ProviderProtocol.AnthropicMessages => """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"hello"}}""",
            ProviderProtocol.GeminiGenerateContent => """{"candidates":[{"index":0,"content":{"parts":[{"text":"hello"}]}}]}""",
            _ => throw new ArgumentOutOfRangeException(nameof(protocol))
        };
        var tail = protocol switch
        {
            ProviderProtocol.OpenAiResponses => """{"type":"response.output_text.delta","delta":"world"}""",
            ProviderProtocol.OpenAiCompatible => """{"choices":[{"index":0,"delta":{"content":"world"},"finish_reason":"stop"}]}""",
            ProviderProtocol.AnthropicMessages => """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"world"}}""",
            ProviderProtocol.GeminiGenerateContent => """{"candidates":[{"index":0,"content":{"parts":[{"text":"world"}]},"finishReason":"STOP"}]}""",
            _ => throw new ArgumentOutOfRangeException(nameof(protocol))
        };
        var terminal = protocol == ProviderProtocol.AnthropicMessages
            ? "data: {\"type\":\"message_stop\"}\n\n" : "data: [DONE]\n\n";
        return new ProviderTlsStreamServer($"data: {prefix}\n\n", $"data: {tail}\n\n{terminal}", token);
    }
}
