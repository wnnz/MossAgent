using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.Anthropic;
using MossAgent.Providers.Gemini;
using MossAgent.Providers.OpenAi;
using MossAgent.Providers.ToolNames;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ProviderToolNameMapTests
{
    [Fact]
    public void Encode_ProducesSafeReversibleNames()
    {
        var dotted = Descriptor("filesystem.write_file");
        var longName = Descriptor(new string('x', 80) + ".tool");
        var safe = Descriptor("safe_tool-1");
        var names = new ProviderToolNameMap([dotted, longName, safe]);

        Assert.Equal("safe_tool-1", names.Encode(safe.Name));
        AssertSafeAndReversible(names, dotted.Name);
        AssertSafeAndReversible(names, longName.Name);
        Assert.NotEqual(names.Encode(dotted.Name), names.Encode(longName.Name));
    }

    [Fact]
    public void RequestBuilders_EncodeDottedToolNames()
    {
        var tool = Descriptor("filesystem.write_file");
        var request = Request(tool);
        var names = new ProviderToolNameMap([tool]);
        var expected = names.Encode(tool.Name);

        using var openAi = JsonDocument.Parse(OpenAiRequestBuilder.Build(request, names));
        using var anthropic = JsonDocument.Parse(AnthropicRequestBuilder.Build(request, names));
        using var gemini = JsonDocument.Parse(GeminiRequestBuilder.Build(request, names));

        Assert.Equal(expected, openAi.RootElement.GetProperty("tools")[0].GetProperty("name").GetString());
        Assert.Equal(expected, anthropic.RootElement.GetProperty("tools")[0].GetProperty("name").GetString());
        Assert.Equal(expected, gemini.RootElement.GetProperty("tools")[0]
            .GetProperty("functionDeclarations")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void OpenAiEventMapper_DecodesToolName()
    {
        var tool = Descriptor("filesystem.write_file");
        var names = new ProviderToolNameMap([tool]);
        var wireName = names.Encode(tool.Name);
        using var eventDocument = JsonDocument.Parse($$"""
            {"type":"response.function_call_arguments.done","call_id":"call-1",
             "name":"{{wireName}}","arguments":"{\"path\":\"probe.txt\"}"}
            """);

        var result = Assert.IsType<ToolCallCompleted>(
            OpenAiEventMapper.Map(eventDocument.RootElement, names));

        Assert.Equal(tool.Name, result.ToolName);
        Assert.Equal("probe.txt", result.Arguments.GetProperty("path").GetString());
    }

    private static void AssertSafeAndReversible(ProviderToolNameMap names, string original)
    {
        var encoded = names.Encode(original);
        Assert.InRange(encoded.Length, 1, 64);
        Assert.Matches("^[a-zA-Z0-9_-]+$", encoded);
        Assert.Equal(original, names.Decode(encoded));
    }

    private static ToolDescriptor Descriptor(string name) => new(
        name, "Test tool", "{\"type\":\"object\"}",
        ToolRiskLevel.ReadOnly, ToolCapability.FileRead);

    private static ModelRequest Request(ToolDescriptor tool)
    {
        var providerId = Guid.NewGuid();
        var provider = new AiProvider(
            providerId, "Test", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1/"), "test-key", null, true, true,
            new Dictionary<string, string>());
        var model = new ModelProfile(
            Guid.NewGuid(), providerId, "test-model", "Test model", 8_192, 1_024,
            "off", null, null, false, false, true, true, "{}");
        return new ModelRequest(
            provider, model, [new ModelMessage(ModelRole.User, "Use the tool")], [tool]);
    }
}
