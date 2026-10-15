using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.Anthropic;
using MossAgent.Providers.Gemini;
using MossAgent.Providers.OpenAi;
using MossAgent.Providers.ToolNames;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ModelAdvancedParametersTests
{
    [Fact]
    public void Responses_PreservesManagedFieldsAndMergesSafeReasoningOptions()
    {
        var request = CreateRequest(ProviderProtocol.OpenAiResponses,
            """{"input":[],"max_output_tokens":999999,"previous_response_id":"fake","temperature":2,"reasoning":{"effort":"high","summary":"auto"},"metadata":{"tag":"safe"}}""");

        using var document = JsonDocument.Parse(OpenAiRequestBuilder.Build(request, new ProviderToolNameMap([])));
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("input").GetArrayLength());
        Assert.Equal(8_192, root.GetProperty("max_output_tokens").GetInt32());
        Assert.False(root.TryGetProperty("previous_response_id", out _));
        Assert.Equal(0.25, root.GetProperty("temperature").GetDouble());
        Assert.Equal(0.75, root.GetProperty("top_p").GetDouble());
        Assert.Equal("low", root.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal("auto", root.GetProperty("reasoning").GetProperty("summary").GetString());
        Assert.Equal("safe", root.GetProperty("metadata").GetProperty("tag").GetString());
    }

    [Fact]
    public void Compatible_PreservesMessagesAndOutputLimit()
    {
        var request = CreateRequest(ProviderProtocol.OpenAiCompatible,
            """{"messages":[],"max_tokens":999999,"temperature":2,"reasoning_effort":"xhigh","metadata":{"tag":"safe"}}""");

        using var document = JsonDocument.Parse(OpenAiCompatibleRequestBuilder.Build(request, new ProviderToolNameMap([])));
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("messages").GetArrayLength());
        Assert.Equal(8_192, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0.25, root.GetProperty("temperature").GetDouble());
        Assert.Equal("low", root.GetProperty("reasoning_effort").GetString());
        Assert.Equal("safe", root.GetProperty("metadata").GetProperty("tag").GetString());
    }

    [Theory]
    [InlineData(4_096, 4_096)]
    [InlineData(999_999, 8_192)]
    public void Compatible_UsesBoundedAlternateCompletionLimit(int requested, int expected)
    {
        var request = CreateRequest(ProviderProtocol.OpenAiCompatible,
            $$"""{"max_completion_tokens":{{requested}}}""");

        using var document = JsonDocument.Parse(OpenAiCompatibleRequestBuilder.Build(request, new ProviderToolNameMap([])));
        var root = document.RootElement;
        Assert.False(root.TryGetProperty("max_tokens", out _));
        Assert.Equal(expected, root.GetProperty("max_completion_tokens").GetInt32());
    }

    [Fact]
    public void Anthropic_PreservesSystemAndThinkingWhileMergingOutputOptions()
    {
        var request = CreateRequest(ProviderProtocol.AnthropicMessages,
            """{"system":"replaced","messages":[],"max_tokens":999999,"thinking":{"type":"disabled"},"output_config":{"effort":"xhigh","format":{"type":"json_schema"}},"metadata":{"user_id":"safe"}}""");

        using var document = JsonDocument.Parse(AnthropicRequestBuilder.Build(request, new ProviderToolNameMap([])));
        var root = document.RootElement;
        Assert.Equal("trusted system", root.GetProperty("system").GetString());
        Assert.Single(root.GetProperty("messages").EnumerateArray());
        Assert.Equal(8_192, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal("adaptive", root.GetProperty("thinking").GetProperty("type").GetString());
        Assert.Equal("low", root.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.Equal("json_schema", root.GetProperty("output_config")
            .GetProperty("format").GetProperty("type").GetString());
        Assert.Equal(0.75, root.GetProperty("top_p").GetDouble());
    }

    [Fact]
    public void Gemini_PreservesSystemAndLimitsWhileMergingGenerationOptions()
    {
        var request = CreateRequest(ProviderProtocol.GeminiGenerateContent,
            """{"systemInstruction":{"parts":[{"text":"replaced"}]},"contents":[],"generationConfig":{"maxOutputTokens":999999,"thinkingConfig":{"thinkingLevel":"HIGH"},"responseMimeType":"application/json"}}""");

        using var document = JsonDocument.Parse(GeminiRequestBuilder.Build(request, new ProviderToolNameMap([])));
        var root = document.RootElement;
        Assert.Equal("trusted system", root.GetProperty("systemInstruction")
            .GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Single(root.GetProperty("contents").EnumerateArray());
        var generation = root.GetProperty("generationConfig");
        Assert.Equal(8_192, generation.GetProperty("maxOutputTokens").GetInt32());
        Assert.Equal("LOW", generation.GetProperty("thinkingConfig")
            .GetProperty("thinkingLevel").GetString());
        Assert.Equal("application/json", generation.GetProperty("responseMimeType").GetString());
    }

    private static ModelRequest CreateRequest(ProviderProtocol protocol, string advancedJson)
    {
        var providerId = Guid.NewGuid();
        var modelId = protocol switch
        {
            ProviderProtocol.AnthropicMessages => "claude-sonnet-4-6",
            ProviderProtocol.GeminiGenerateContent => "gemini-3.8-flash",
            _ => "test-model"
        };
        var provider = new AiProvider(providerId, "test", protocol,
            new Uri("https://example.test/v1/"), "test-key", null, true, true,
            new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), providerId, modelId, modelId,
            128_000, 8_192, "low", 0.25, 0.75, false, false, true, true, advancedJson);
        return new ModelRequest(provider, model,
            [new ModelMessage(ModelRole.System, "trusted system"),
                new ModelMessage(ModelRole.User, "hello")], []);
    }
}
