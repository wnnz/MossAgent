using System.Net;
using MossAgent.Application.Models;
using MossAgent.Domain;
using static MossAgent.Providers.Tests.ProviderStreamingTestFixture;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class OpenAiResponseTerminalEventTests
{
    [Theory]
    [InlineData("{\"error\":null}")]
    [InlineData("{\"error\":\"untrusted text\"}")]
    [InlineData("{\"error\":{\"code\":42,\"message\":null}}")]
    public async Task FailedResponseWithoutAUsableErrorStillFailsWithSafeFallback(string response)
    {
        var body = $"data: {{\"type\":\"response.failed\",\"response\":{response}}}\n\n";
        var events = await CollectAsync(ProviderProtocol.OpenAiResponses,
            new RecordingResponseHandler(HttpStatusCode.OK, body, contentType: "text/event-stream"), TestContext.Current.CancellationToken);
        var failure = Assert.IsType<ModelFailed>(Assert.Single(events));
        Assert.Equal("openai_error", failure.Code);
        Assert.DoesNotContain("untrusted text", failure.Message);
    }

    [Fact]
    public async Task NestedResponseFailureIsSanitizedAndStopsLaterTextAndToolEvents()
    {
        const string body = """
            data: {"type":"response.output_text.delta","delta":"partial"}

            data: {"type":"response.failed","response":{"status":"failed","error":{"code":"server_error","message":"fixture-api-secret"}}}

            data: {"type":"response.function_call_arguments.done","name":"shell.execute","call_id":"late","arguments":"{}"}

            data: {"type":"response.output_text.delta","delta":"must not appear"}

            data: [DONE]

            """;
        var events = await CollectAsync(ProviderProtocol.OpenAiResponses,
            new RecordingResponseHandler(HttpStatusCode.OK, body, contentType: "text/event-stream"), TestContext.Current.CancellationToken);
        Assert.Equal(2, events.Count);
        Assert.Equal("partial", Assert.IsType<TextDelta>(events[0]).Text);
        var failure = Assert.IsType<ModelFailed>(events[1]);
        Assert.Equal("server_error", failure.Code);
        Assert.DoesNotContain("fixture-api-secret", failure.Message);
        Assert.Contains("***", failure.Message);
    }

    [Theory]
    [InlineData("max_output_tokens")]
    [InlineData("content_filter")]
    [InlineData("unknown-untrusted-reason")]
    public async Task IncompleteResponsesAreFailuresAndCannotBeMistakenForNormalCompletion(string reason)
    {
        var body = $"data: {{\"type\":\"response.incomplete\",\"response\":{{\"status\":\"incomplete\",\"incomplete_details\":{{\"reason\":\"{reason}\"}}}}}}\n\n";
        var events = await CollectAsync(ProviderProtocol.OpenAiResponses,
            new RecordingResponseHandler(HttpStatusCode.OK, body, contentType: "text/event-stream"), TestContext.Current.CancellationToken);
        var failure = Assert.IsType<ModelFailed>(Assert.Single(events));
        Assert.Equal("response_incomplete", failure.Code);
        Assert.DoesNotContain(reason, failure.Message);
    }
}
