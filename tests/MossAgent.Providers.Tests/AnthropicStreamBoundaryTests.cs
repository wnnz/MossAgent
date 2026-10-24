using System.Net;
using System.Text;
using MossAgent.Application.Models;
using MossAgent.Domain;
using static MossAgent.Providers.Tests.ProviderStreamingTestFixture;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class AnthropicStreamBoundaryTests
{
    private const string ToolStart = """
        {"type":"content_block_start","index":1,"content_block":{"type":"tool_use","id":"tool-1","name":"probe"}}
        """;

    [Fact]
    public async Task PendingToolUseBlock_AtEof_FailsWithoutPublishingTheCall()
    {
        var body = Frame(ToolStart) + Frame("""
            {"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{\"path\":\""}}
            """);

        var events = await Collect(body);

        Assert.Equal("incomplete_response", Assert.IsType<ModelFailed>(Assert.Single(events)).Code);
    }

    [Fact]
    public async Task PendingToolUseBlock_AtMessageStop_FailsAsInvalidResponse()
    {
        var body = Frame(ToolStart) + Frame("""
            {"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{\"path\":\"a.txt\"}"}}
            """) + Frame("{\"type\":\"message_stop\"}");

        var events = await Collect(body);

        Assert.Equal("invalid_response", Assert.IsType<ModelFailed>(Assert.Single(events)).Code);
    }

    [Theory]
    [InlineData("data: not-json\n\n")]
    [InlineData("data: [DONE]\n\n")]
    [InlineData("data: []\n\n")]
    [InlineData("data: {\"type\":\n\n")]
    public async Task MalformedOrNonObjectFrames_FailAsInvalidResponse(string body)
    {
        var events = await Collect(body);

        Assert.Equal("invalid_response", Assert.IsType<ModelFailed>(Assert.Single(events)).Code);
    }

    [Fact]
    public async Task InvalidToolArguments_AtBlockStop_FailsAsInvalidToolArguments()
    {
        var body = Frame(ToolStart) + Frame("""
            {"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{broken"}}
            """) + Frame("{\"type\":\"content_block_stop\",\"index\":1}");

        var events = await Collect(body);

        Assert.Equal("invalid_tool_arguments", Assert.IsType<ModelFailed>(Assert.Single(events)).Code);
    }

    [Theory]
    [InlineData("{\"type\":\"tool_use\",\"name\":\"probe\"}")]
    [InlineData("{\"type\":\"tool_use\",\"id\":\"tool-1\"}")]
    [InlineData("{\"type\":\"tool_use\",\"id\":\"tool-1\",\"name\":42}")]
    public async Task ToolBlockStart_WithoutUsableIdentity_FailsAsInvalidToolArguments(string block)
    {
        var body = Frame($"{{\"type\":\"content_block_start\",\"index\":1,\"content_block\":{block}}}");

        var events = await Collect(body);

        Assert.Equal("invalid_tool_arguments", Assert.IsType<ModelFailed>(Assert.Single(events)).Code);
    }

    [Theory]
    [InlineData("max_tokens", "output_limit")]
    [InlineData("refusal", "provider_blocked")]
    public async Task AbnormalMessageDeltaStopReason_FailsInsteadOfCompleting(string reason, string code)
    {
        var body = Frame("""
            {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"partial"}}
            """) + Frame($"{{\"type\":\"message_delta\",\"delta\":{{\"stop_reason\":\"{reason}\"}}}}")
            + Frame("{\"type\":\"message_stop\"}");

        var events = await Collect(body);

        Assert.Equal("partial", Assert.IsType<TextDelta>(events[0]).Text);
        Assert.Equal(code, Assert.IsType<ModelFailed>(events[1]).Code);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task OrdinaryMessageDelta_BeforeMessageStop_StillCompletes()
    {
        var body = Frame("""
            {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"answer"}}
            """) + Frame("{\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"}}")
            + Frame("{\"type\":\"message_stop\"}");

        var events = await Collect(body);

        Assert.Equal("answer", Assert.IsType<TextDelta>(events[0]).Text);
        Assert.IsType<ModelCompleted>(events[1]);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task ErrorEvent_IsTerminalAndStopsLaterFrames()
    {
        var body = Frame("""
            {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"partial"}}
            """) + Frame("{\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"busy\"}}")
            + Frame("""
                {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"late"}}
                """) + Frame("{\"type\":\"message_stop\"}");

        var events = await Collect(body);

        Assert.Equal("partial", Assert.IsType<TextDelta>(events[0]).Text);
        Assert.Equal("overloaded_error", Assert.IsType<ModelFailed>(events[1]).Code);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task CancellationWithPendingToolBlock_DoesNotPublishOrComplete()
    {
        var handler = new ProviderByteResponseHandler(Encoding.UTF8.GetBytes(
            Frame(ToolStart) + Frame("""
                {"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{\"path\":"}}
                """)), blockAtEnd: true);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var events = CreateProvider(ProviderProtocol.AnthropicMessages, handler)
            .StreamAsync(CreateRequest(ProviderProtocol.AnthropicMessages), cancellation.Token)
            .GetAsyncEnumerator(cancellation.Token);
        var pending = events.MoveNextAsync().AsTask();
        await handler.ResponseStream!.BlockedRead.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(handler.ResponseStream.IsDisposed);
    }

    private static string Frame(string json) => $"data: {json}\n\n";

    private static Task<List<ModelEvent>> Collect(string body) =>
        CollectAsync(ProviderProtocol.AnthropicMessages,
            new RecordingResponseHandler(HttpStatusCode.OK, body, contentType: "text/event-stream"),
            TestContext.Current.CancellationToken);
}
