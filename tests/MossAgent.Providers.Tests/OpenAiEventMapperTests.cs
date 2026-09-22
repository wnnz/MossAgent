using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Providers.OpenAi;
using MossAgent.Providers.ToolNames;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class OpenAiEventMapperTests
{
    [Fact]
    public void Map_HandlesTextToolCompletionAndErrorEvents()
    {
        var descriptor = new ToolDescriptor(
            "filesystem.read_file", "read", "{\"type\":\"object\"}",
            ToolRiskLevel.ReadOnly, ToolCapability.FileRead);
        var names = new ProviderToolNameMap([descriptor]);
        var wireName = names.Encode(descriptor.Name);

        var text = Assert.IsType<TextDelta>(Map(
            "{\"type\":\"response.output_text.delta\",\"delta\":\"hello\"}", names));
        Assert.Equal("hello", text.Text);

        var tool = Assert.IsType<ToolCallCompleted>(Map($$"""
            {"type":"response.function_call_arguments.done","call_id":"call-1",
             "name":"{{wireName}}","arguments":"{\"path\":\"a.txt\"}"}
            """, names));
        Assert.Equal(descriptor.Name, tool.ToolName);
        Assert.Equal("a.txt", tool.Arguments.GetProperty("path").GetString());

        var completed = Assert.IsType<ModelCompleted>(Map(
            "{\"type\":\"response.completed\",\"response\":{\"id\":\"resp-1\"}}", names));
        Assert.Equal("resp-1", completed.ResponseId);

        var error = Assert.IsType<ModelFailed>(Map(
            "{\"type\":\"error\",\"error\":{\"code\":\"bad\",\"message\":\"failed\"}}",
            names));
        Assert.Equal("bad", error.Code);
        Assert.Equal("failed", error.Message);
    }

    private static ModelEvent? Map(string json, ProviderToolNameMap names)
    {
        using var document = JsonDocument.Parse(json);
        return OpenAiEventMapper.Map(document.RootElement, names);
    }
}
