using System.Text.Json;
using MossAgent.Application.Models;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class GeminiStreamStateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionalSentinel_StillRequiresStop(bool stopped)
    {
        var payload = GeminiStreamTestData.Frame([GeminiStreamTestData.Call()], stopped ? "STOP" : null) + "data: [DONE]\n\n";

        var events = await Collect(payload);

        if (!stopped) GeminiStreamBoundaryTests.AssertFailure(events, "incomplete_response");
        else
        {
            Assert.Single(events.OfType<ToolCallCompleted>());
            Assert.Single(events.OfType<ModelCompleted>());
            Assert.Empty(events.OfType<ModelFailed>());
        }
    }

    [Fact]
    public async Task ErrorAfterStop_DiscardsToolsAndDoesNotParseFurtherEvents()
    {
        var payload = GeminiStreamTestData.Frame([GeminiStreamTestData.Call()], "STOP")
            + "data: " + JsonSerializer.Serialize(new { error = new { code = 429, message = GeminiStreamTestData.Secret } }) + "\n\n"
            + GeminiStreamTestData.Frame([new { text = "must not appear" }], "STOP");

        var events = await Collect(payload);

        GeminiStreamBoundaryTests.AssertFailure(events, "429");
        Assert.Empty(events.OfType<TextDelta>());
    }

    [Fact]
    public async Task ThoughtText_IsNotPublishedAsTheAssistantAnswer()
    {
        var payload = GeminiStreamTestData.Frame([new { text = "internal", thought = true }, new { text = "answer", thought = false }], "STOP");

        var events = await Collect(payload);

        Assert.Equal("answer", Assert.Single(events.OfType<TextDelta>()).Text);
        Assert.Single(events.OfType<ModelCompleted>());
    }

    [Fact]
    public async Task OtherCandidateCompletion_CannotCompleteTheSelectedCandidate()
    {
        const string payload = "data: {\"candidates\":[{\"index\":1,\"content\":{\"parts\":[{\"text\":\"other\"}]},\"finishReason\":\"STOP\"}]}\n\n";

        var events = await Collect(payload);

        GeminiStreamBoundaryTests.AssertFailure(events, "incomplete_response");
        Assert.Empty(events.OfType<TextDelta>());
    }

    [Theory]
    [InlineData("data: {}\n\n")]
    [InlineData("data: {\"candidates\":[]}\n\n")]
    [InlineData("data: {\"promptFeedback\":{\"blockReason\":\"BLOCK_REASON_UNSPECIFIED\"}}\n\n")]
    public async Task MetadataWithoutSelectedStop_IsNotSuccessful(string payload)
    {
        GeminiStreamBoundaryTests.AssertFailure(await Collect(payload), "incomplete_response");
    }

    [Fact]
    public async Task MissingArguments_UseAnEmptyObjectAndRetainTheResponseIdentity()
    {
        var payload = "data: " + JsonSerializer.Serialize(new
        {
            responseId = "gemini-response-id",
            candidates = new[] { new { content = new { parts = new[] { new { functionCall = new { name = GeminiStreamTestData.WireName } } } }, finishReason = "STOP" } }
        }) + "\n\n";

        var events = await Collect(payload);

        Assert.Equal("{}", Assert.Single(events.OfType<ToolCallCompleted>()).Arguments.GetRawText());
        Assert.Equal("gemini-response-id", Assert.Single(events.OfType<ModelCompleted>()).ResponseId);
    }

    private static Task<List<ModelEvent>> Collect(string payload) =>
        GeminiStreamTestData.CollectAsync(payload, TestContext.Current.CancellationToken);
}
