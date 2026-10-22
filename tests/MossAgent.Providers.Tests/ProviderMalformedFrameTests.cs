using System.Net;
using MossAgent.Application.Models;
using MossAgent.Domain;
using static MossAgent.Providers.Tests.ProviderStreamingTestFixture;
using Xunit;

namespace MossAgent.Providers.Tests;

/// <summary>
/// Deterministic fixtures for frames a gateway, proxy or broken provider can actually send:
/// unparsable JSON, incomplete tool calls and repeated terminal signals. Each case must end in a
/// bounded <see cref="ModelFailed"/> or exactly one <see cref="ModelCompleted"/>, never an exception
/// escaping the stream or a partially executed tool call.
/// </summary>
public sealed class ProviderMalformedFrameTests
{
    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public async Task UnparsableFrameFailsTheTurnInsteadOfThrowing(ProviderProtocol protocol)
    {
        var events = await CollectAsync(protocol, new RecordingResponseHandler(HttpStatusCode.OK,
            "data: {\"type\":\"response.completed\",\n\n", contentType: "text/event-stream"),
            TestContext.Current.CancellationToken);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(events));
        Assert.Equal("invalid_response", failure.Code);
    }

    [Theory]
    [InlineData("{\"type\":\"response.function_call_arguments.done\",\"name\":7,\"call_id\":\"c1\",\"arguments\":\"{}\"}")]
    [InlineData("{\"type\":\"response.function_call_arguments.done\",\"name\":\"read_file\",\"call_id\":\"c1\"}")]
    [InlineData("{\"type\":\"response.function_call_arguments.done\",\"name\":\"read_file\",\"call_id\":\"c1\",\"arguments\":\"{oops\"}")]
    [InlineData("{\"type\":\"response.output_item.done\",\"item\":{\"type\":\"function_call\",\"name\":\"read_file\",\"call_id\":\"c1\",\"arguments\":\"not-json\"}}")]
    public async Task MalformedResponsesToolCallNeverReachesThePipeline(string frame)
    {
        var events = await CollectAsync(ProviderProtocol.OpenAiResponses, new RecordingResponseHandler(
            HttpStatusCode.OK, $"data: {frame}\n\n", contentType: "text/event-stream"),
            TestContext.Current.CancellationToken);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(events));
        Assert.Equal("invalid_tool_arguments", failure.Code);
        Assert.DoesNotContain(events, item => item is ToolCallCompleted);
    }

    [Theory]
    [InlineData("{\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"tool_use\",\"id\":\"c1\",\"name\":9}}")]
    [InlineData("{\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"tool_use\",\"name\":\"read_file\"}}")]
    public async Task MalformedAnthropicToolBlockFailsBeforeArgumentsArrive(string frame)
    {
        var events = await CollectAsync(ProviderProtocol.AnthropicMessages, new RecordingResponseHandler(
            HttpStatusCode.OK, $"data: {frame}\n\n", contentType: "text/event-stream"),
            TestContext.Current.CancellationToken);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(events));
        Assert.Equal("invalid_tool_arguments", failure.Code);
    }

    [Fact]
    public async Task AnthropicInvalidToolArgumentsFailAtBlockStop()
    {
        const string body = """
            data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"c1","name":"read_file"}}

            data: {"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{oops"}}

            data: {"type":"content_block_stop","index":0}

            """;
        var events = await CollectAsync(ProviderProtocol.AnthropicMessages, new RecordingResponseHandler(
            HttpStatusCode.OK, body, contentType: "text/event-stream"), TestContext.Current.CancellationToken);

        var failure = Assert.IsType<ModelFailed>(Assert.Single(events));
        Assert.Equal("invalid_tool_arguments", failure.Code);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses,
        "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"first\"}}\n\ndata: {\"type\":\"response.completed\",\"response\":{\"id\":\"second\"}}\n\n")]
    [InlineData(ProviderProtocol.OpenAiCompatible, "data: {\"choices\":[{\"index\":0,\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\ndata: [DONE]\n\n")]
    [InlineData(ProviderProtocol.AnthropicMessages, "data: {\"type\":\"message_stop\"}\n\ndata: {\"type\":\"message_stop\"}\n\n")]
    [InlineData(ProviderProtocol.GeminiGenerateContent,
        "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"done\"}]},\"finishReason\":\"STOP\"}]}\n\ndata: {\"candidates\":[{\"finishReason\":\"STOP\"}]}\n\n")]
    public async Task RepeatedTerminalSignalsProduceExactlyOneCompletion(ProviderProtocol protocol, string body)
    {
        var events = await CollectAsync(protocol, new RecordingResponseHandler(HttpStatusCode.OK, body,
            contentType: "text/event-stream"), TestContext.Current.CancellationToken);

        Assert.IsType<ModelCompleted>(Assert.Single(events, item => item is ModelCompleted));
        Assert.DoesNotContain(events, item => item is ModelFailed);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    public async Task CancellationBeforeReadingNeverEmitsACompletion(ProviderProtocol protocol)
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var events = new List<ModelEvent>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in CreateProvider(protocol, new RecordingResponseHandler(HttpStatusCode.OK,
                               "data: {\"type\":\"message_stop\"}\n\n", contentType: "text/event-stream"))
                           .StreamAsync(CreateRequest(protocol), cancellation.Token))
                events.Add(item);
        });
        Assert.DoesNotContain(events, item => item is ModelCompleted);
    }
}
