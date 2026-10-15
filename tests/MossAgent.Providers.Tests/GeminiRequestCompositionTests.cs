using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.Gemini;
using MossAgent.Providers.ToolNames;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class GeminiRequestCompositionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(64)]
    [InlineData(128)]
    public void ToolResultsShareOneHistoryIndexRegardlessOfBatchSize(int count)
    {
        var calls = Enumerable.Range(0, count).Select(index => Call($"call-{index}", $"native-{index}")).ToArray();
        var messages = new CountingModelMessageList(new[] { new ModelMessage(ModelRole.Assistant, "", ToolCalls: calls) }
            .Concat(calls.Select(call => Result(call.CallId))).ToArray());

        using var output = Build(messages);

        var contents = output.RootElement.GetProperty("contents");
        Assert.Equal(2, contents.GetArrayLength());
        var parts = contents[1].GetProperty("parts");
        Assert.Equal(count, parts.GetArrayLength());
        for (var index = 0; index < count; index++)
        {
            var response = parts[index].GetProperty("functionResponse");
            Assert.Equal($"native-{index}", response.GetProperty("id").GetString());
            Assert.Equal($"call-{index}", response.GetProperty("response").GetProperty("call").GetString());
        }
        Assert.Equal(3, messages.EnumerationCount);
    }

    [Fact]
    public void HistoryWithoutToolResultsDoesNotBuildAnIndex()
    {
        var messages = new CountingModelMessageList([
            new(ModelRole.System, "instructions"),
            new(ModelRole.User, "question"),
            new(ModelRole.Assistant, "", ToolCalls: [Call("call", "native")])]);

        using var output = Build(messages);

        Assert.Equal(2, output.RootElement.GetProperty("contents").GetArrayLength());
        Assert.Equal(2, messages.EnumerationCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("native-first")]
    public void DuplicateCallIdsRetainTheFirstMatchEvenWhenItsNativeIdIsMissing(string? firstNativeId)
    {
        var messages = new CountingModelMessageList([
            new(ModelRole.Assistant, "", ToolCalls: [Call("call", firstNativeId)]),
            new(ModelRole.Assistant, "", ToolCalls: [Call("call", "native-later")]),
            Result("call")]);

        using var output = Build(messages);

        var response = output.RootElement.GetProperty("contents")[2].GetProperty("parts")[0].GetProperty("functionResponse");
        if (firstNativeId is null) Assert.False(response.TryGetProperty("id", out _));
        else Assert.Equal(firstNativeId, response.GetProperty("id").GetString());
    }

    [Theory]
    [InlineData("CALL")]
    [InlineData("missing")]
    [InlineData(null)]
    public void UnknownOrDifferentlyCasedCallIdsDoNotInventNativeIdentity(string? callId)
    {
        using var output = Build([
            new(ModelRole.Assistant, "", ToolCalls: [Call("call", "native")]),
            Result(callId)]);

        var response = output.RootElement.GetProperty("contents")[1].GetProperty("parts")[0].GetProperty("functionResponse");
        Assert.False(response.TryGetProperty("id", out _));
    }

    [Theory]
    [InlineData(ModelRole.User)]
    [InlineData(ModelRole.System)]
    [InlineData(ModelRole.ToolObservation)]
    [InlineData(ModelRole.Tool)]
    public void OnlyAssistantCallsProvideNativeIdentity(ModelRole role)
    {
        using var output = Build([
            new(role, "data", ToolCalls: [Call("call", "foreign-native")]),
            Result("call")]);

        var contents = output.RootElement.GetProperty("contents");
        var response = contents[contents.GetArrayLength() - 1].GetProperty("parts");
        Assert.False(response[response.GetArrayLength() - 1].GetProperty("functionResponse").TryGetProperty("id", out _));
    }

    [Fact]
    public void ConsecutiveToolResultsPreserveOrderAndSystemMessagesDoNotSplitTheirGroup()
    {
        using var output = Build([
            new(ModelRole.Assistant, "", ToolCalls: [Call("one", "native-one"), Call("two", "native-two")]),
            Result("one"), new(ModelRole.System, "instructions"), Result("two"),
            new(ModelRole.User, "question"), Result("three"),
            new(ModelRole.ToolObservation, "observation"), Result("four")]);

        var contents = output.RootElement.GetProperty("contents");
        Assert.Equal(6, contents.GetArrayLength());
        var grouped = contents[1].GetProperty("parts");
        Assert.Equal(2, grouped.GetArrayLength());
        Assert.Equal("native-one", grouped[0].GetProperty("functionResponse").GetProperty("id").GetString());
        Assert.Equal("native-two", grouped[1].GetProperty("functionResponse").GetProperty("id").GetString());
        Assert.Equal("question", contents[2].GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Single(contents[3].GetProperty("parts").EnumerateArray());
        Assert.Equal("observation", contents[4].GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Single(contents[5].GetProperty("parts").EnumerateArray());
        Assert.Equal("instructions", output.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
    }

    [Fact]
    public void ResultsBeforeTheirCallKeepTheExistingWholeHistoryLookupBehavior()
    {
        using var output = Build([Result("call"), new(ModelRole.Assistant, "", ToolCalls: [Call("call", "native")])]);

        var response = output.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("functionResponse");
        Assert.Equal("native", response.GetProperty("id").GetString());
    }

    [Fact]
    public void RebuildingARequestReadsItsCurrentHistoryInsteadOfSharingAnOldIndex()
    {
        ModelMessage[] history = [new(ModelRole.Assistant, "", ToolCalls: [Call("call", "old-native")]), Result("call")];
        var messages = new CountingModelMessageList(history);
        using var first = Build(messages);
        history[0] = new(ModelRole.Assistant, "", ToolCalls: [Call("call", "new-native")]);

        using var second = Build(messages);

        Assert.Equal("old-native", first.RootElement.GetProperty("contents")[1].GetProperty("parts")[0]
            .GetProperty("functionResponse").GetProperty("id").GetString());
        Assert.Equal("new-native", second.RootElement.GetProperty("contents")[1].GetProperty("parts")[0]
            .GetProperty("functionResponse").GetProperty("id").GetString());
        Assert.Equal(6, messages.EnumerationCount);
    }

    private static ModelToolCall Call(string callId, string? nativeId) =>
        new(callId, GeminiStreamTestData.Tool.Name, JsonSerializer.SerializeToElement(new { path = "示例.txt" }), nativeId);

    private static ModelMessage Result(string? callId) => new(ModelRole.Tool,
        JsonSerializer.Serialize(new { call = callId }), callId, GeminiStreamTestData.Tool.Name);

    private static JsonDocument Build(IReadOnlyList<ModelMessage> messages)
    {
        var request = GeminiStreamTestData.Request() with { Messages = messages };
        return JsonDocument.Parse(GeminiRequestBuilder.Build(request, new ProviderToolNameMap(request.Tools)));
    }
}
