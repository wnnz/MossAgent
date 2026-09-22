using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.Anthropic;
using MossAgent.Providers.ToolNames;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class AnthropicStreamParserTests
{
    [Fact]
    public void Parse_AssemblesToolArgumentsAndDecodesName()
    {
        var descriptor = new ToolDescriptor(
            "filesystem.read_file", "read", "{\"type\":\"object\"}",
            ToolRiskLevel.ReadOnly, ToolCapability.FileRead);
        var names = new ProviderToolNameMap([descriptor]);
        var parser = new AnthropicStreamParser(names);
        Parse(parser, $$$"""
            {"type":"content_block_start","index":1,"content_block":
             {"type":"tool_use","id":"tool-1","name":"{{{names.Encode(descriptor.Name)}}}"}}
            """);
        Parse(parser, """
            {"type":"content_block_delta","index":1,
             "delta":{"type":"input_json_delta","partial_json":"{\"path\":\"a.txt\"}"}}
            """);

        var result = Assert.IsType<ToolCallCompleted>(Parse(
            parser, "{\"type\":\"content_block_stop\",\"index\":1}"));

        Assert.Equal("tool-1", result.CallId);
        Assert.Equal(descriptor.Name, result.ToolName);
        Assert.Equal("a.txt", result.Arguments.GetProperty("path").GetString());
    }

    [Fact]
    public void Parse_MapsTextAndCompletion()
    {
        var parser = new AnthropicStreamParser(new ProviderToolNameMap([]));

        var text = Assert.IsType<TextDelta>(Parse(parser, """
            {"type":"content_block_delta","index":0,
             "delta":{"type":"text_delta","text":"hello"}}
            """));
        Assert.Equal("hello", text.Text);
        Assert.IsType<ModelCompleted>(Parse(parser, "{\"type\":\"message_stop\"}"));
    }

    private static ModelEvent? Parse(AnthropicStreamParser parser, string json)
    {
        using var document = JsonDocument.Parse(json);
        return parser.Parse(document.RootElement);
    }
}
