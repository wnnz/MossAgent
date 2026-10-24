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

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public async Task KeepaliveOnlyStream_IsNotACompletedModelTurn(ProviderProtocol protocol)
    {
        var events = await CollectAsync(protocol, new RecordingResponseHandler(HttpStatusCode.OK,
            ": keepalive\n\n: another\n\n", contentType: "text/event-stream"),
            TestContext.Current.CancellationToken);
        Assert.IsType<ModelFailed>(Assert.Single(events));
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses,
        "{\"type\":\"response.completed\",\"response\":{\"id\":\"first\"}}",
        "{\"type\":\"response.completed\",\"response\":{\"id\":\"second\"}}",
        "{\"type\":\"response.output_text.delta\",\"delta\":\"late\"}")]
    [InlineData(ProviderProtocol.AnthropicMessages,
        "{\"type\":\"message_stop\"}",
        "{\"type\":\"message_stop\"}",
        "{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"late\"}}")]
    [InlineData(ProviderProtocol.OpenAiCompatible,
        "{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"hi\"},\"finish_reason\":\"stop\"}]}",
        "[DONE]",
        "{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"late\"}}]}")]
    [InlineData(ProviderProtocol.GeminiGenerateContent,
        "{\"candidates\":[{\"index\":0,\"content\":{\"parts\":[{\"text\":\"hi\"}]},\"finishReason\":\"STOP\"}]}",
        "{\"candidates\":[{\"index\":0,\"content\":{\"parts\":[]},\"finishReason\":\"STOP\"}]}",
        "")]
    public async Task DuplicateTerminal_EmitsOneCompletionAndStopsBeforeLaterFrames(
        ProviderProtocol protocol, string terminal, string duplicate, string late)
    {
        var body = $"data: {terminal}\n\ndata: {duplicate}\n\n";
        if (late.Length > 0) body += $"data: {late}\n\n";

        var events = await CollectAsync(protocol, new RecordingResponseHandler(HttpStatusCode.OK, body,
            contentType: "text/event-stream"), TestContext.Current.CancellationToken);

        Assert.Single(events.OfType<ModelCompleted>());
        Assert.Empty(events.OfType<ModelFailed>());
        Assert.DoesNotContain(events, item => item is TextDelta { Text: "late" });
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses,
        "{\"type\":\"response.failed\",\"response\":{\"status\":\"failed\",\"error\":{\"code\":\"boom\",\"message\":\"x\"}}}",
        "{\"type\":\"response.completed\",\"response\":{\"id\":\"late\"}}")]
    [InlineData(ProviderProtocol.AnthropicMessages,
        "{\"type\":\"error\",\"error\":{\"type\":\"boom\",\"message\":\"x\"}}",
        "{\"type\":\"message_stop\"}")]
    [InlineData(ProviderProtocol.OpenAiCompatible,
        "{\"error\":{\"code\":\"boom\",\"message\":\"x\"}}",
        "[DONE]")]
    [InlineData(ProviderProtocol.GeminiGenerateContent,
        "{\"error\":{\"code\":500,\"message\":\"x\"}}",
        "{\"candidates\":[{\"index\":0,\"content\":{\"parts\":[{\"text\":\"late\"}]},\"finishReason\":\"STOP\"}]}")]
    public async Task LateTerminalAfterFailure_DoesNotAddACompletion(
        ProviderProtocol protocol, string failure, string late)
    {
        var body = $"data: {failure}\n\ndata: {late}\n\n";

        var events = await CollectAsync(protocol, new RecordingResponseHandler(HttpStatusCode.OK, body,
            contentType: "text/event-stream"), TestContext.Current.CancellationToken);

        Assert.IsType<ModelFailed>(Assert.Single(events));
    }
}
