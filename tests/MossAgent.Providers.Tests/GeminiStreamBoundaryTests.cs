using System.Text;
using System.Text.Json;
using MossAgent.Application.Models;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class GeminiStreamBoundaryTests
{
    [Fact]
    public async Task PrematureEof_DiscardsPendingToolsAndProducesOneSafeFailure()
    {
        var payload = GeminiStreamTestData.Frame([new { text = "partial" }, GeminiStreamTestData.Call()]);

        AssertFailure(await Collect(payload), "incomplete_response");
    }

    [Theory]
    [InlineData("MAX_TOKENS", "output_limit")]
    [InlineData("SAFETY", "provider_blocked")]
    [InlineData("RECITATION", "provider_blocked")]
    [InlineData("BLOCKLIST", "provider_blocked")]
    [InlineData("PROHIBITED_CONTENT", "provider_blocked")]
    [InlineData("SPII", "provider_blocked")]
    [InlineData("IMAGE_SAFETY", "provider_blocked")]
    [InlineData("IMAGE_PROHIBITED_CONTENT", "provider_blocked")]
    [InlineData("IMAGE_RECITATION", "provider_blocked")]
    [InlineData("ESCALATION", "provider_blocked")]
    [InlineData("PUP_LIMITED_DISABLED", "provider_blocked")]
    [InlineData("MALFORMED_FUNCTION_CALL", "invalid_tool_arguments")]
    [InlineData("UNEXPECTED_TOOL_CALL", "invalid_tool_arguments")]
    [InlineData("TOO_MANY_TOOL_CALLS", "tool_calls_limit")]
    [InlineData("MISSING_THOUGHT_SIGNATURE", "invalid_response")]
    [InlineData("OTHER", "invalid_response")]
    [InlineData("FINISH_REASON_UNSPECIFIED", "invalid_response")]
    [InlineData("future-finish-" + GeminiStreamTestData.Secret, "invalid_response")]
    public async Task AbnormalFinish_CannotAuthorizeThePendingToolBatch(string finish, string code)
    {
        var payload = GeminiStreamTestData.Frame([GeminiStreamTestData.Call()], finish);

        AssertFailure(await Collect(payload), code);
    }

    [Theory]
    [InlineData("SAFETY")]
    [InlineData("OTHER")]
    [InlineData("BLOCKLIST")]
    [InlineData("PROHIBITED_CONTENT")]
    public async Task PromptFeedbackBlocking_IsNotAnEmptySuccessfulAnswer(string reason)
    {
        var payload = GeminiStreamTestData.Frame([GeminiStreamTestData.Call()])
            + "data: " + JsonSerializer.Serialize(new { promptFeedback = new { blockReason = reason } }) + "\n\n";

        AssertFailure(await Collect(payload), "provider_blocked");
    }

    [Theory]
    [InlineData("data: {\n\n")]
    [InlineData("data: []\n\n")]
    [InlineData("data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"late\"}]}}]}\n\n")]
    public async Task InvalidTailAfterStop_DiscardsAllPendingTools(string tail)
    {
        var payload = GeminiStreamTestData.Frame([GeminiStreamTestData.Call()], "STOP") + tail;

        AssertFailure(await Collect(payload), "invalid_response");
    }

    [Fact]
    public async Task LateCallAfterStop_IsRejectedBeforePublishingAnyTool()
    {
        var payload = GeminiStreamTestData.Frame([GeminiStreamTestData.Call()], "STOP")
            + GeminiStreamTestData.Frame([GeminiStreamTestData.Call()]);

        AssertFailure(await Collect(payload), "invalid_response");
    }

    [Fact]
    public async Task MetadataAfterStop_CompletesOnceAndKeepsTheToolArguments()
    {
        var payload = GeminiStreamTestData.Frame([new { text = "partial" }, GeminiStreamTestData.Call()], "STOP")
            + "data: {\"candidates\":[{\"index\":0,\"content\":{\"parts\":[]}}],\"usageMetadata\":{\"totalTokenCount\":12}}\n\n";

        var events = await Collect(payload);

        Assert.Single(events.OfType<ModelCompleted>());
        Assert.Empty(events.OfType<ModelFailed>());
        Assert.Equal("partial", Assert.Single(events.OfType<TextDelta>()).Text);
        var call = Assert.Single(events.OfType<ToolCallCompleted>());
        Assert.Equal("filesystem.write_file", call.ToolName);
        Assert.Equal("fixture.txt", call.Arguments.GetProperty("path").GetString());
        Assert.IsType<ModelCompleted>(events[^1]);
    }

    [Fact]
    public async Task CancellationBeforeEof_DoesNotPublishOrCompletePendingTools()
    {
        var handler = new ProviderByteResponseHandler(Encoding.UTF8.GetBytes(
            GeminiStreamTestData.Frame([GeminiStreamTestData.Call()], "STOP")), blockAtEnd: true);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var events = GeminiStreamTestData.Provider(handler)
            .StreamAsync(GeminiStreamTestData.Request(), cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        var pending = events.MoveNextAsync().AsTask();
        await handler.ResponseStream!.BlockedRead.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(handler.ResponseStream.IsDisposed);
    }

    internal static void AssertFailure(IReadOnlyList<ModelEvent> events, string code)
    {
        var failure = Assert.Single(events.OfType<ModelFailed>());
        Assert.Equal(code, failure.Code);
        Assert.DoesNotContain(GeminiStreamTestData.Secret, failure.Message);
        Assert.Empty(events.OfType<ToolCallCompleted>());
        Assert.Empty(events.OfType<ModelCompleted>());
    }

    private static Task<List<ModelEvent>> Collect(string payload) =>
        GeminiStreamTestData.CollectAsync(payload, TestContext.Current.CancellationToken);
}
