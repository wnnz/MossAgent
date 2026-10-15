using System.Net;
using System.Text;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.Streaming;
using static MossAgent.Providers.Tests.ProviderStreamingTestFixture;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ProviderResponseLimitTests
{
    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public async Task OversizedSseLine_BecomesOneSafeFailureAndDisposesTheResponse(ProviderProtocol protocol)
    {
        var payload = "data: fixture-api-secret " + new string('x', SseLineReader.MaximumLineCharacters * 2);
        var handler = new ProviderByteResponseHandler(Encoding.UTF8.GetBytes(payload));

        var failure = Assert.IsType<ModelFailed>(Assert.Single(await CollectAsync(protocol, handler)));

        Assert.Equal("stream_line_limit", failure.Code);
        Assert.DoesNotContain("fixture-api-secret", failure.Message);
        Assert.DoesNotContain(new string('x', 40), failure.Message);
        Assert.True(handler.ResponseStream!.BytesRead <= SseLineReader.MaximumLineCharacters + 4096);
        Assert.True(handler.ResponseStream.IsDisposed);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public async Task MultilineEvent_AlsoUsesTheSharedBudget(ProviderProtocol protocol)
    {
        var line = "data:" + new string('x', 1023) + "\n";
        var payload = string.Concat(Enumerable.Repeat(line, SseDataReader.MaximumEventCharacters / 1024 + 2));
        var handler = new ProviderByteResponseHandler(Encoding.UTF8.GetBytes(payload));

        var failure = Assert.IsType<ModelFailed>(Assert.Single(await CollectAsync(protocol, handler)));

        Assert.Equal("stream_event_limit", failure.Code);
        Assert.True(handler.ResponseStream!.IsDisposed);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public async Task InvalidUtf8_DoesNotReachTheProtocolParser(ProviderProtocol protocol)
    {
        var handler = new ProviderByteResponseHandler([.. Encoding.UTF8.GetBytes("data: "), 0xC3, 0x28]);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(await CollectAsync(protocol, handler)));

        Assert.Equal("invalid_stream_encoding", failure.Code);
        Assert.True(handler.ResponseStream!.IsDisposed);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, true)]
    [InlineData(ProviderProtocol.OpenAiCompatible, true)]
    [InlineData(ProviderProtocol.AnthropicMessages, true)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, true)]
    [InlineData(ProviderProtocol.OpenAiResponses, false)]
    [InlineData(ProviderProtocol.OpenAiCompatible, false)]
    [InlineData(ProviderProtocol.AnthropicMessages, false)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, false)]
    public async Task HttpErrorBodies_AreBoundedEvenWithoutContentLength(ProviderProtocol protocol, bool declaredLength)
    {
        var payload = "fixture-api-secret " + new string('x', ProviderHttpErrorReader.MaximumBodyBytes * 2);
        var handler = new ProviderByteResponseHandler(Encoding.UTF8.GetBytes(payload), HttpStatusCode.BadGateway, declaredLength);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(await CollectAsync(protocol, handler)));

        Assert.Equal("http_error", failure.Code);
        Assert.StartsWith("HTTP 502:", failure.Message);
        Assert.DoesNotContain("fixture-api-secret", failure.Message);
        Assert.DoesNotContain(new string('x', 40), failure.Message);
        Assert.Equal(declaredLength ? 0 : ProviderHttpErrorReader.MaximumBodyBytes + 1, handler.ResponseStream!.BytesRead);
        Assert.True(handler.ResponseStream.IsDisposed);
    }

    [Fact]
    public async Task ExactHttpErrorLimit_IsAcceptedAndSanitized()
    {
        const string prefix = "fixture-api-secret ";
        var payload = prefix + new string('x', ProviderHttpErrorReader.MaximumBodyBytes - prefix.Length);
        var handler = new ProviderByteResponseHandler(Encoding.UTF8.GetBytes(payload), HttpStatusCode.BadRequest);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(await CollectAsync(ProviderProtocol.OpenAiCompatible, handler)));

        Assert.Equal("http_error", failure.Code);
        Assert.Contains("***", failure.Message);
        Assert.DoesNotContain("fixture-api-secret", failure.Message);
        Assert.True(failure.Message.Length <= 512);
        Assert.Equal(ProviderHttpErrorReader.MaximumBodyBytes, handler.ResponseStream!.BytesRead);
    }

    [Fact]
    public async Task InvalidUtf8HttpError_IsReplacedWithoutEchoingBytes()
    {
        var handler = new ProviderByteResponseHandler([0xC3, 0x28], HttpStatusCode.BadRequest);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(await CollectAsync(ProviderProtocol.OpenAiCompatible, handler)));

        Assert.Equal("http_error", failure.Code);
        Assert.Contains("UTF-8", failure.Message);
        Assert.True(handler.ResponseStream!.IsDisposed);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, HttpStatusCode.OK)]
    [InlineData(ProviderProtocol.OpenAiCompatible, HttpStatusCode.OK)]
    [InlineData(ProviderProtocol.AnthropicMessages, HttpStatusCode.OK)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, HttpStatusCode.OK)]
    [InlineData(ProviderProtocol.OpenAiResponses, HttpStatusCode.BadRequest)]
    [InlineData(ProviderProtocol.OpenAiCompatible, HttpStatusCode.BadRequest)]
    [InlineData(ProviderProtocol.AnthropicMessages, HttpStatusCode.BadRequest)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, HttpStatusCode.BadRequest)]
    public async Task CancellingResponseRead_PropagatesAndDisposesTheResponse(
        ProviderProtocol protocol, HttpStatusCode status)
    {
        var handler = new ProviderByteResponseHandler([], status, blockAtEnd: true);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var provider = CreateProvider(protocol, handler);
        await using var events = provider.StreamAsync(CreateRequest(protocol), cancellation.Token)
            .GetAsyncEnumerator(cancellation.Token);
        var pending = events.MoveNextAsync().AsTask();
        await handler.ResponseStream!.BlockedRead.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(handler.ResponseStream.IsDisposed);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public async Task EndingEnumerationEarly_DisposesWithoutWaitingForEof(ProviderProtocol protocol)
    {
        var handler = new ProviderByteResponseHandler(
            Encoding.UTF8.GetBytes($"data: {CreateTextPayload(protocol)}\n\n"), blockAtEnd: true);
        var provider = CreateProvider(protocol, handler);
        await using var events = provider.StreamAsync(CreateRequest(protocol), TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        Assert.True(await events.MoveNextAsync().AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Equal("hello", Assert.IsType<TextDelta>(events.Current).Text);
        Assert.False(handler.ResponseStream!.IsDisposed);

        await events.DisposeAsync();

        Assert.True(handler.ResponseStream.IsDisposed);
        Assert.False(handler.ResponseStream.BlockedRead.Task.IsCompleted);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public async Task HttpFailure_DoesNotEnterTheSseParser(ProviderProtocol protocol)
    {
        var handler = new ProviderByteResponseHandler(
            Encoding.UTF8.GetBytes("data: not-json fixture-api-secret\n\n"), HttpStatusCode.BadRequest);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(await CollectAsync(protocol, handler)));

        Assert.Equal("http_error", failure.Code);
        Assert.StartsWith("HTTP 400:", failure.Message);
        Assert.DoesNotContain("fixture-api-secret", failure.Message);
        Assert.True(handler.ResponseStream!.IsDisposed);
    }

    private static string CreateTextPayload(ProviderProtocol protocol) => protocol switch
    {
        ProviderProtocol.OpenAiResponses => """{"type":"response.output_text.delta","delta":"hello"}""",
        ProviderProtocol.OpenAiCompatible => """{"choices":[{"index":0,"delta":{"content":"hello"}}]}""",
        ProviderProtocol.AnthropicMessages => """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"hello"}}""",
        ProviderProtocol.GeminiGenerateContent => """{"candidates":[{"index":0,"content":{"parts":[{"text":"hello"}]}}]}""",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol))
    };

    private static Task<List<ModelEvent>> CollectAsync(ProviderProtocol protocol, ProviderByteResponseHandler handler)
        => ProviderStreamingTestFixture.CollectAsync(protocol, handler, TestContext.Current.CancellationToken);
}
