using System.Text.Json;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class GeminiResponseValidationTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"candidates\":42}")]
    [InlineData("{\"candidates\":[null]}")]
    [InlineData("{\"candidates\":[{\"index\":-1}]}")]
    [InlineData("{\"candidates\":[{\"index\":\"0\"}]}")]
    [InlineData("{\"candidates\":[{\"content\":\"invalid\"}]}")]
    [InlineData("{\"candidates\":[{\"content\":{\"parts\":42}}]}")]
    [InlineData("{\"candidates\":[{\"content\":{\"parts\":[42]}}]}")]
    [InlineData("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":42}]}}]}")]
    [InlineData("{\"candidates\":[{\"finishReason\":42}]}")]
    [InlineData("{\"candidates\":[{\"finishReason\":\"\"}]}")]
    [InlineData("{\"promptFeedback\":42}")]
    [InlineData("{\"promptFeedback\":{\"blockReason\":42}}")]
    [InlineData("{\"candidates\":[{\"index\":0},{\"index\":0}]}")]
    public async Task InvalidShapes_ProduceSafeFailuresRatherThanThrowing(string json)
    {
        var events = await GeminiStreamTestData.CollectAsync("data: " + json + "\n\n", TestContext.Current.CancellationToken);

        GeminiStreamBoundaryTests.AssertFailure(events, "invalid_response");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("\"invalid\"")]
    public async Task InvalidArguments_RejectTheWholeBatchIncludingTheFirstValidCall(string arguments)
    {
        using var document = JsonDocument.Parse(arguments);
        var payload = GeminiStreamTestData.Frame([GeminiStreamTestData.Call(),
            new { functionCall = new { name = GeminiStreamTestData.WireName, args = document.RootElement } }], "STOP");

        var events = await GeminiStreamTestData.CollectAsync(payload, TestContext.Current.CancellationToken);

        GeminiStreamBoundaryTests.AssertFailure(events, "invalid_tool_arguments");
    }
}
