using System.Net;
using MossAgent.Application.Models;
using MossAgent.Domain;
using static MossAgent.Providers.Tests.ProviderStreamingTestFixture;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ProviderTerminalBoundaryTests
{
    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, "data: {\"type\":\"response.output_text.delta\",\"delta\":\"partial\"}\n\n")]
    [InlineData(ProviderProtocol.AnthropicMessages, "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"partial\"}}\n\n")]
    [InlineData(ProviderProtocol.OpenAiCompatible, "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"partial\"}}]}\n\n")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"partial\"}]}}]}\n\n")]
    public async Task PartialStreamWithoutItsTerminalSignalFailsInsteadOfCompleting(ProviderProtocol protocol, string body)
    {
        var events = await CollectAsync(protocol, new RecordingResponseHandler(HttpStatusCode.OK, body,
            contentType: "text/event-stream"), TestContext.Current.CancellationToken);
        Assert.Contains(events, item => item is TextDelta { Text: "partial" });
        Assert.Contains(events, item => item is ModelFailed);
        Assert.DoesNotContain(events, item => item is ModelCompleted);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public async Task EmptySuccessfulHttpResponseIsNotACompletedModelTurn(ProviderProtocol protocol)
    {
        var events = await CollectAsync(protocol, new RecordingResponseHandler(HttpStatusCode.OK, string.Empty,
            contentType: "text/event-stream"), TestContext.Current.CancellationToken);
        Assert.IsType<ModelFailed>(Assert.Single(events));
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, "{\"type\":\"response.completed\",\"response\":{\"id\":\"completed\"}}",
        "{\"type\":\"response.output_text.delta\",\"delta\":\"late\"}")]
    [InlineData(ProviderProtocol.AnthropicMessages, "{\"type\":\"message_stop\"}",
        "{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"late\"}}")]
    public async Task ExplicitCompletionStopsBeforeLateFramesAndDoesNotRequireTransportEnd(ProviderProtocol protocol,
        string completion, string lateText)
    {
        var body = $"data: {completion}\n\ndata: {lateText}\n\n";
        var events = await CollectAsync(protocol, new RecordingResponseHandler(HttpStatusCode.OK, body,
            contentType: "text/event-stream"), TestContext.Current.CancellationToken);
        Assert.IsType<ModelCompleted>(Assert.Single(events));
    }
}
