using System.Net;
using MossAgent.Application.Models;
using MossAgent.Domain;
using static MossAgent.Providers.Tests.ProviderStreamingTestFixture;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ProviderFailureResendBoundaryTests
{
    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, "data: {\"type\":\"error\",\"error\":{\"code\":\"boom\",\"message\":\"nope\"}}\n\n")]
    [InlineData(ProviderProtocol.OpenAiCompatible, "data: {\"error\":{\"code\":\"boom\",\"message\":\"nope\"}}\n\n")]
    [InlineData(ProviderProtocol.AnthropicMessages, "data: {\"type\":\"error\",\"error\":{\"type\":\"boom\",\"message\":\"nope\"}}\n\n")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "data: {\"error\":{\"code\":500,\"message\":\"nope\"}}\n\n")]
    public async Task FailureEvent_SendsExactlyOneRequest(ProviderProtocol protocol, string body)
    {
        var handler = new RecordingResponseHandler(HttpStatusCode.OK, body, contentType: "text/event-stream");

        var events = await CollectAsync(protocol, handler, TestContext.Current.CancellationToken);

        Assert.IsType<ModelFailed>(Assert.Single(events));
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, "data: {\"type\":\"response.output_text.delta\",\"delta\":\"partial\"}\n\n")]
    [InlineData(ProviderProtocol.OpenAiCompatible, "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"partial\"}}]}\n\n")]
    [InlineData(ProviderProtocol.AnthropicMessages, "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"partial\"}}\n\n")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"partial\"}]}}]}\n\n")]
    public async Task TruncatedStream_SendsExactlyOneRequest(ProviderProtocol protocol, string body)
    {
        var handler = new RecordingResponseHandler(HttpStatusCode.OK, body, contentType: "text/event-stream");

        var events = await CollectAsync(protocol, handler, TestContext.Current.CancellationToken);

        Assert.Equal("partial", Assert.IsType<TextDelta>(events[0]).Text);
        Assert.IsType<ModelFailed>(events[^1]);
        Assert.DoesNotContain(events, item => item is ModelCompleted);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public async Task HttpError_SendsExactlyOneRequest(ProviderProtocol protocol)
    {
        var handler = new RecordingResponseHandler(HttpStatusCode.InternalServerError, "server error");

        var events = await CollectAsync(protocol, handler, TestContext.Current.CancellationToken);

        Assert.Equal("http_error", Assert.IsType<ModelFailed>(Assert.Single(events)).Code);
        Assert.Equal(1, handler.RequestCount);
    }
}
