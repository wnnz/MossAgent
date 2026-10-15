using MossAgent.Application.Models;
using MossAgent.Providers.OpenAi;
using MossAgent.Providers.ToolNames;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class OpenAiCompatibleFinishMetadataTests
{
    [Theory]
    [InlineData("stop", "[]")]
    [InlineData("tool_calls", "[]")]
    [InlineData("stop", "null")]
    [InlineData("tool_calls", "null")]
    public void HarmlessMetadataAfterFinish_DoesNotFailOrDuplicateCalls(string finish, string calls)
    {
        var parser = CreateParser();
        Assert.Empty(parser.Parse("""
            {"choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"one-call",
            "function":{"name":"probe","arguments":"{}"}}]}}]}
            """));
        var events = parser.Parse($"{{\"choices\":[{{\"index\":0,\"delta\":{{}},\"finish_reason\":\"{finish}\"}}]}}").ToList();
        Assert.Empty(parser.Parse($"{{\"choices\":[{{\"index\":0,\"delta\":{{\"content\":\"\",\"tool_calls\":{calls}}}}}]}}"));
        Assert.Empty(parser.Parse("{\"choices\":[],\"usage\":{\"total_tokens\":12}}"));
        events.AddRange(parser.Parse("[DONE]"));

        Assert.Single(events.OfType<ToolCallCompleted>());
        Assert.Single(events.OfType<ModelCompleted>());
        Assert.Empty(events.OfType<ModelFailed>());
        Assert.Empty(parser.Complete());
        Assert.Empty(parser.Parse("[DONE]"));
    }

    [Theory]
    [InlineData("{\"tool_calls\":{}}")]
    [InlineData("{\"tool_calls\":0}")]
    [InlineData("{\"tool_calls\":[{\"index\":0}]}")]
    [InlineData("{\"content\":\"late text\"}")]
    public void LateDataOrMalformedMetadata_StillFailsClosed(string delta)
    {
        var parser = CreateParser();
        Assert.Empty(parser.Parse("{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}"));

        var failure = Assert.IsType<ModelFailed>(Assert.Single(parser.Parse(
            $"{{\"choices\":[{{\"index\":0,\"delta\":{delta}}}]}}")));

        Assert.Equal("invalid_response", failure.Code);
        Assert.Empty(parser.Parse("[DONE]"));
        Assert.Empty(parser.Complete());
    }

    private static OpenAiCompatibleStreamParser CreateParser() => new(new ProviderToolNameMap([
        new ToolDescriptor("probe", "fixture", "{\"type\":\"object\"}", ToolRiskLevel.ReadOnly, ToolCapability.None)]));
}
