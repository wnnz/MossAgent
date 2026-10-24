using System.Net;
using MossAgent.Application.Models;
using MossAgent.Domain;
using static MossAgent.Providers.Tests.ProviderStreamingTestFixture;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class OpenAiResponsesStreamBoundaryTests
{
    [Theory]
    [InlineData("data: [DONE]\n\n")]
    [InlineData("data: {\"type\":\"response.output_text.delta\",\"delta\":\"partial\"}\n\ndata: [DONE]\n\n")]
    public async Task DoneSentinel_WithoutTerminalEvent_FailsAsIncomplete(string body)
    {
        var events = await CollectAsync(ProviderProtocol.OpenAiResponses, body);

        Assert.Equal("incomplete_response", Assert.IsType<ModelFailed>(events[^1]).Code);
        Assert.Empty(events.OfType<ModelCompleted>());
    }

    [Theory]
    [InlineData("data: not-json\n\n")]
    [InlineData("data: {\"type\":\n\n")]
    [InlineData("data: []\n\n")]
    [InlineData("data: 42\n\n")]
    [InlineData("data: \n\n")]
    public async Task MalformedOrNonObjectFrames_FailAsInvalidResponse(string body)
    {
        var events = await CollectAsync(ProviderProtocol.OpenAiResponses, body);

        Assert.Equal("invalid_response", Assert.IsType<ModelFailed>(Assert.Single(events)).Code);
    }

    [Theory]
    [InlineData("{\"type\":\"mystery\"}")]
    [InlineData("{\"type\":42}")]
    [InlineData("{}")]
    [InlineData("{\"type\":\"response.output_text.delta\",\"delta\":42}")]
    public async Task IgnorableFrames_StillRequireARealTerminal(string frame)
    {
        var events = await CollectAsync(ProviderProtocol.OpenAiResponses, $"data: {frame}\n\n");

        Assert.Equal("incomplete_response", Assert.IsType<ModelFailed>(Assert.Single(events)).Code);
    }

    [Theory]
    [InlineData("{\"type\":\"response.function_call_arguments.done\",\"call_id\":\"c\",\"name\":\"probe\",\"arguments\":\"{\"}")]
    [InlineData("{\"type\":\"response.function_call_arguments.done\",\"call_id\":\"c\",\"name\":\"probe\",\"arguments\":42}")]
    [InlineData("{\"type\":\"response.function_call_arguments.done\",\"call_id\":\"c\",\"arguments\":\"{}\"}")]
    [InlineData("{\"type\":\"response.function_call_arguments.done\",\"name\":\"probe\",\"arguments\":\"{}\"}")]
    [InlineData("{\"type\":\"response.function_call_arguments.done\",\"call_id\":\"c\",\"name\":42,\"arguments\":\"{}\"}")]
    [InlineData("{\"type\":\"response.output_item.done\",\"item\":{\"type\":\"function_call\",\"call_id\":\"c\",\"name\":\"probe\",\"arguments\":\"[}\"}}")]
    public async Task MalformedFunctionCallDone_FailsAsInvalidToolArguments(string frame)
    {
        var events = await CollectAsync(ProviderProtocol.OpenAiResponses, $"data: {frame}\n\n");

        Assert.Equal("invalid_tool_arguments", Assert.IsType<ModelFailed>(Assert.Single(events)).Code);
    }

    [Fact]
    public async Task FunctionCallDone_WithoutArguments_UsesAnEmptyObject()
    {
        const string body = """
            data: {"type":"response.function_call_arguments.done","call_id":"c","name":"probe"}

            data: {"type":"response.completed","response":{"id":"done"}}

            """;

        var events = await CollectAsync(ProviderProtocol.OpenAiResponses, body);

        var call = Assert.IsType<ToolCallCompleted>(events[0]);
        Assert.Empty(call.Arguments.EnumerateObject());
        Assert.IsType<ModelCompleted>(events[1]);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task ArgumentsDelta_WithoutDone_IsNotPublishedAsACall()
    {
        const string body = """
            data: {"type":"response.function_call_arguments.delta","call_id":"c","delta":"{\"path\":"}

            data: {"type":"response.function_call_arguments.delta","call_id":"c","delta":"\"a.txt\"}"}

            data: {"type":"response.completed","response":{"id":"done"}}

            """;

        var events = await CollectAsync(ProviderProtocol.OpenAiResponses, body);

        Assert.IsType<ModelCompleted>(Assert.Single(events));
    }

    [Fact]
    public async Task ArgumentsDelta_WithoutDone_OrTerminal_FailsAtEof()
    {
        const string body = """
            data: {"type":"response.function_call_arguments.delta","call_id":"c","delta":"{\"path\":"}

            """;

        var events = await CollectAsync(ProviderProtocol.OpenAiResponses, body);

        Assert.Equal("incomplete_response", Assert.IsType<ModelFailed>(Assert.Single(events)).Code);
    }

    private static Task<List<ModelEvent>> CollectAsync(ProviderProtocol protocol, string body) =>
        ProviderStreamingTestFixture.CollectAsync(protocol,
            new RecordingResponseHandler(HttpStatusCode.OK, body, contentType: "text/event-stream"),
            TestContext.Current.CancellationToken);
}
