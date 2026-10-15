using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.OpenAi;
using MossAgent.Providers.ToolNames;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class OpenAiCompatibleFrameAggregationTests
{
    [Fact]
    public void MultipleSelectedEntries_PreserveTextThenToolOrderAndIgnoreOtherChoices()
    {
        var parser = CreateParser();
        var events = parser.Parse("""
            {"id":"response","choices":[
              {"index":0,"delta":{"content":"first"}},
              {"index":1,"delta":{"content":"ignored"},"finish_reason":"stop"},
              {"index":0,"delta":{"content":"second","tool_calls":[
                {"index":0,"id":"call","function":{"name":"probe","arguments":"{}"}}]},
                "finish_reason":"tool_calls"}]}
            """);

        Assert.Equal(3, events.Count);
        Assert.Equal("first", Assert.IsType<TextDelta>(events[0]).Text);
        Assert.Equal("second", Assert.IsType<TextDelta>(events[1]).Text);
        Assert.Equal("call", Assert.IsType<ToolCallCompleted>(events[2]).CallId);
        Assert.Equal("response", Assert.IsType<ModelCompleted>(Assert.Single(parser.Parse("[DONE]"))).ResponseId);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("{\"index\":-1}")]
    [InlineData("{\"index\":0,\"delta\":42}")]
    [InlineData("{\"index\":0,\"delta\":{\"tool_calls\":{}}}")]
    [InlineData("{\"index\":0,\"delta\":{\"tool_calls\":[{\"function\":42}]}}")]
    public void LaterInvalidEntry_DiscardsEarlierTextInTheSameFrame(string invalid)
    {
        var parser = CreateParser();
        var events = parser.Parse("{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"discarded\"}}," + invalid + "]}");

        Assert.Equal("invalid_response", Assert.IsType<ModelFailed>(Assert.Single(events)).Code);
        Assert.True(parser.IsTerminal);
        Assert.Empty(parser.Parse("[DONE]"));
        Assert.Empty(parser.Complete());
    }

    [Fact]
    public void InvalidEntryAfterToolFinish_DoesNotPublishTheEarlierCompletedCall()
    {
        var parser = CreateParser();
        var events = parser.Parse("""
            {"choices":[{"index":0,"delta":{"content":"discarded","tool_calls":[
              {"index":0,"id":"discarded","function":{"name":"probe","arguments":"{}"}}]},
              "finish_reason":"tool_calls"},{"index":0,"delta":42}]}
            """);

        Assert.Equal("invalid_response", Assert.IsType<ModelFailed>(Assert.Single(events)).Code);
        Assert.Empty(parser.Complete());
    }

    [Fact]
    public void InvalidToolArguments_DiscardsAllEarlierTextAndCallsInTheSameFrame()
    {
        var parser = CreateParser();
        var events = parser.Parse(JsonSerializer.Serialize(new
        {
            choices = new object[]
            {
                new { index = 0, delta = new { content = "discarded", tool_calls = new[] {
                    new { index = 0, id = "valid", function = new { name = "probe", arguments = "{}" } } } } },
                new { index = 0, delta = new { tool_calls = new[] {
                    new { index = 1, id = "invalid", function = new { name = "probe", arguments = "{" } } } },
                    finish_reason = "tool_calls" }
            }
        }));

        Assert.Equal("invalid_tool_arguments", Assert.IsType<ModelFailed>(Assert.Single(events)).Code);
        Assert.Empty(parser.Parse("[DONE]"));
    }

    [Fact]
    public void LaterFrames_DoNotMutatePreviouslyReturnedEvents()
    {
        var parser = CreateParser();
        var first = parser.Parse("{\"choices\":[{\"delta\":{\"content\":\"first\"}}]}");
        var second = parser.Parse("{\"choices\":[{\"delta\":{\"content\":\"second\"}}]}");

        Assert.Equal("first", Assert.IsType<TextDelta>(Assert.Single(first)).Text);
        Assert.Equal("second", Assert.IsType<TextDelta>(Assert.Single(second)).Text);
        Assert.NotSame(first, second);
    }

    private static OpenAiCompatibleStreamParser CreateParser() => new(new ProviderToolNameMap([
        new ToolDescriptor("probe", "fixture", "{}", ToolRiskLevel.ReadOnly, ToolCapability.None)]));
}
