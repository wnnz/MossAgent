using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.Anthropic;
using MossAgent.Providers.ToolNames;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class AnthropicToolCallStateTests
{
    [Fact]
    public void InterleavedBlocks_RetainTheirOwnIdentityAndArguments()
    {
        var parser = CreateParser();
        Start(parser, 2, "second", "second_tool");
        Start(parser, 1, "first", "first_tool");
        Append(parser, 2, "{\"value\":\"");
        Append(parser, 1, "{\"value\":\"");
        Append(parser, 2, "第二\"}");
        Append(parser, 1, "第一\"}");

        AssertCall(Stop(parser, 2), "second", "second_tool", "第二");
        AssertCall(Stop(parser, 1), "first", "first_tool", "第一");
        Assert.Null(Stop(parser, 2));
        Assert.Null(Stop(parser, 1));
    }

    [Fact]
    public void UnknownIndices_CannotChangeAnotherBlock()
    {
        var parser = CreateParser();
        Start(parser, 1, "owned", "first_tool");
        Append(parser, 2, "{\"value\":\"wrong\"}");
        Assert.Null(Stop(parser, 2));
        Append(parser, 1, "{\"value\":\"owned\"}");

        AssertCall(Stop(parser, 1), "owned", "first_tool", "owned");
    }

    [Fact]
    public void RestartedIndex_ReplacesIdentityAndPendingArgumentsTogether()
    {
        var parser = CreateParser();
        Start(parser, 0, "old", "first_tool");
        Append(parser, 0, "{\"unfinished\":");
        Start(parser, 0, "new", "second_tool");
        Append(parser, 0, "{\"value\":\"new\"}");

        AssertCall(Stop(parser, 0), "new", "second_tool", "new");
    }

    [Fact]
    public void StoppedIndex_CanBeReusedWithoutOldArguments()
    {
        var parser = CreateParser();
        Start(parser, 0, "old", "first_tool");
        Append(parser, 0, "{\"value\":\"old\"}");
        var first = Stop(parser, 0);
        Start(parser, 0, "new", "second_tool");

        var second = Assert.IsType<ToolCallCompleted>(Stop(parser, 0));

        AssertCall(first, "old", "first_tool", "old");
        Assert.Equal("new", second.CallId);
        Assert.Equal("second_tool", second.ToolName);
        Assert.Empty(second.Arguments.EnumerateObject());
    }

    [Fact]
    public void InvalidArguments_RemoveOnlyTheirOwnBlockAndReportAFailureInsteadOfThrowing()
    {
        var parser = CreateParser();
        Start(parser, 0, "invalid", "first_tool");
        Start(parser, 1, "valid", "second_tool");
        Append(parser, 0, "{");
        Append(parser, 1, "{\"value\":\"valid\"}");

        // Unparsable arguments now surface as a bounded failure event: an exception would escape the
        // provider stream and abort the run without a reviewable state.
        var failure = Assert.IsType<ModelFailed>(Stop(parser, 0));
        Assert.Equal("invalid_tool_arguments", failure.Code);

        Assert.Null(Stop(parser, 0));
        AssertCall(Stop(parser, 1), "valid", "second_tool", "valid");
    }

    [Fact]
    public void SeparateParsers_DoNotSharePendingBlocks()
    {
        var first = CreateParser();
        var second = CreateParser();
        Start(first, 0, "first", "first_tool");
        Start(second, 0, "second", "second_tool");
        Append(first, 0, "{\"value\":\"first\"}");
        Append(second, 0, "{\"value\":\"second\"}");

        AssertCall(Stop(first, 0), "first", "first_tool", "first");
        AssertCall(Stop(second, 0), "second", "second_tool", "second");
    }

    private static AnthropicStreamParser CreateParser()
    {
        var names = new ProviderToolNameMap([
            new ToolDescriptor("first_tool", "fixture", "{}", ToolRiskLevel.ReadOnly, ToolCapability.None),
            new ToolDescriptor("second_tool", "fixture", "{}", ToolRiskLevel.ReadOnly, ToolCapability.None)]);
        return new AnthropicStreamParser(names);
    }

    private static void Start(AnthropicStreamParser parser, int index, string id, string name) =>
        Assert.Null(Parse(parser, new
        {
            type = "content_block_start",
            index,
            content_block = new { type = "tool_use", id, name }
        }));

    private static void Append(AnthropicStreamParser parser, int index, string arguments) =>
        Assert.Null(Parse(parser, new
        {
            type = "content_block_delta",
            index,
            delta = new { type = "input_json_delta", partial_json = arguments }
        }));

    private static ModelEvent? Stop(AnthropicStreamParser parser, int index) =>
        Parse(parser, new { type = "content_block_stop", index });

    private static ModelEvent? Parse(AnthropicStreamParser parser, object payload)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        return parser.Parse(document.RootElement);
    }

    private static void AssertCall(ModelEvent? result, string id, string name, string value)
    {
        var call = Assert.IsType<ToolCallCompleted>(result);
        Assert.Equal(id, call.CallId);
        Assert.Equal(name, call.ToolName);
        Assert.Equal(value, call.Arguments.GetProperty("value").GetString());
    }
}
