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
    public void Responses_MergesSafeReasoningOptionsAndMetadata()
    {
        var request = CreateRequest(ProviderProtocol.OpenAiResponses,
            """{"reasoning":{"summary":"auto"},"metadata":{"tag":"safe"}}""");

        using var document = JsonDocument.Parse(OpenAiRequestBuilder.Build(request, new ProviderToolNameMap([])));
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("input").GetArrayLength());
        Assert.Equal(8_192, root.GetProperty("max_output_tokens").GetInt32());
        Assert.Equal(0.25, root.GetProperty("temperature").GetDouble());
        Assert.Equal(0.75, root.GetProperty("top_p").GetDouble());
        Assert.Equal("low", root.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal("auto", root.GetProperty("reasoning").GetProperty("summary").GetString());
        Assert.Equal("safe", root.GetProperty("metadata").GetProperty("tag").GetString());
    }

    [Theory]
    [InlineData("""{"input":[]}""")]
    [InlineData("""{"max_output_tokens":999999}""")]
    [InlineData("""{"previous_response_id":"fake"}""")]
    [InlineData("""{"temperature":2}""")]
    [InlineData("""{"tools":[]}""")]
    [InlineData("""{"model":"override"}""")]
    [InlineData("""{"stream":false}""")]
    [InlineData("""{"reasoning":{"effort":"high"}}""")]
    [InlineData("""{"reasoning":"high"}""")]
    public void Responses_RejectsManagedOrMalformedOverrides(string advancedJson)
    {
        var request = CreateRequest(ProviderProtocol.OpenAiResponses, advancedJson);

        var error = Assert.Throws<ModelCapabilityException>(
            () => OpenAiRequestBuilder.Build(request, new ProviderToolNameMap([])));

        Assert.Equal("invalid_model_parameters", error.Code);
        Assert.DoesNotContain("test-key", error.Message);
    }

    [Fact]
    public void Compatible_MergesSafeExtensionParameters()
    {
        var request = CreateRequest(ProviderProtocol.OpenAiCompatible,
            """{"metadata":{"tag":"safe"},"vendor_param":123}""");

        using var document = JsonDocument.Parse(OpenAiCompatibleRequestBuilder.Build(request, new ProviderToolNameMap([])));
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("messages").GetArrayLength());
        Assert.Equal(8_192, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0.25, root.GetProperty("temperature").GetDouble());
        Assert.Equal("low", root.GetProperty("reasoning_effort").GetString());
        Assert.Equal("safe", root.GetProperty("metadata").GetProperty("tag").GetString());
        Assert.Equal(123, root.GetProperty("vendor_param").GetInt32());
    }

    [Theory]
    [InlineData("""{"messages":[]}""")]
    [InlineData("""{"max_tokens":999999}""")]
    [InlineData("""{"temperature":2}""")]
    [InlineData("""{"reasoning_effort":"xhigh"}""")]
    [InlineData("""{"system":"replaced"}""")]
    public void Compatible_RejectsManagedOverrides(string advancedJson)
    {
        var request = CreateRequest(ProviderProtocol.OpenAiCompatible, advancedJson);

        var error = Assert.Throws<ModelCapabilityException>(
            () => OpenAiCompatibleRequestBuilder.Build(request, new ProviderToolNameMap([])));

        Assert.Equal("invalid_model_parameters", error.Code);
    }

    [Theory]
    [InlineData(4_096)]
    [InlineData(8_192)]
    public void Compatible_UsesBoundedAlternateCompletionLimit(int requested)
    {
        var request = CreateRequest(ProviderProtocol.OpenAiCompatible,
            $$"""{"max_completion_tokens":{{requested}}}""");

        using var document = JsonDocument.Parse(OpenAiCompatibleRequestBuilder.Build(request, new ProviderToolNameMap([])));
        var root = document.RootElement;
        Assert.False(root.TryGetProperty("max_tokens", out _));
        Assert.Equal(requested, root.GetProperty("max_completion_tokens").GetInt32());
    }

    [Theory]
    [InlineData("""{"max_completion_tokens":999999}""")]
    [InlineData("""{"max_completion_tokens":8193}""")]
    [InlineData("""{"max_completion_tokens":0}""")]
    [InlineData("""{"max_completion_tokens":"8192"}""")]
    public void Compatible_RejectsOutOfRangeAlternateCompletionLimit(string advancedJson)
    {
        var request = CreateRequest(ProviderProtocol.OpenAiCompatible, advancedJson);

        var error = Assert.Throws<ModelCapabilityException>(
            () => OpenAiCompatibleRequestBuilder.Build(request, new ProviderToolNameMap([])));

        Assert.Equal("invalid_model_parameters", error.Code);
        Assert.Contains("max_completion_tokens", error.Message);
    }

    [Fact]
    public void Anthropic_MergesSafeOutputOptionsAndMetadata()
    {
        var request = CreateRequest(ProviderProtocol.AnthropicMessages,
            """{"output_config":{"format":{"type":"json_schema"}},"metadata":{"user_id":"safe"}}""");

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

    [Theory]
    [InlineData("""{"system":"replaced"}""")]
    [InlineData("""{"messages":[]}""")]
    [InlineData("""{"max_tokens":999999}""")]
    [InlineData("""{"thinking":{"type":"disabled"}}""")]
    [InlineData("""{"output_config":{"effort":"xhigh"}}""")]
    [InlineData("""{"output_config":"high"}""")]
    public void Anthropic_RejectsManagedOrMalformedOverrides(string advancedJson)
    {
        var request = CreateRequest(ProviderProtocol.AnthropicMessages, advancedJson);

        var error = Assert.Throws<ModelCapabilityException>(
            () => AnthropicRequestBuilder.Build(request, new ProviderToolNameMap([])));

        Assert.Equal("invalid_model_parameters", error.Code);
    }

    [Fact]
    public void Gemini_MergesSafeGenerationOptions()
    {
        var request = CreateRequest(ProviderProtocol.GeminiGenerateContent,
            """{"generationConfig":{"responseMimeType":"application/json","candidateCount":1}}""");

        using var document = JsonDocument.Parse(GeminiRequestBuilder.Build(request, new ProviderToolNameMap([])));
        var root = document.RootElement;
        Assert.Equal("trusted system", root.GetProperty("systemInstruction")
            .GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Single(root.GetProperty("contents").EnumerateArray());
        var generation = root.GetProperty("generationConfig");
        Assert.Equal(8_192, generation.GetProperty("maxOutputTokens").GetInt32());
        Assert.Equal("application/json", generation.GetProperty("responseMimeType").GetString());
        Assert.Equal(1, generation.GetProperty("candidateCount").GetInt32());
    }

    [Theory]
    [InlineData("""{"systemInstruction":{"parts":[{"text":"replaced"}]}}""")]
    [InlineData("""{"contents":[]}""")]
    [InlineData("""{"generationConfig":{"maxOutputTokens":999999}}""")]
    [InlineData("""{"generationConfig":{"temperature":2}}""")]
    [InlineData("""{"generationConfig":{"thinkingConfig":{"thinkingLevel":"HIGH"}}}""")]
    [InlineData("""{"generationConfig":"fast"}""")]
    public void Gemini_RejectsManagedOrMalformedOverrides(string advancedJson)
    {
        var request = CreateRequest(ProviderProtocol.GeminiGenerateContent, advancedJson);

        var error = Assert.Throws<ModelCapabilityException>(
            () => GeminiRequestBuilder.Build(request, new ProviderToolNameMap([])));

        Assert.Equal("invalid_model_parameters", error.Code);
    }

    [Theory]
    [InlineData("""{"unclosed":""")]
    [InlineData("""[1,2,3]""")]
    [InlineData("""["a"]""")]
    [InlineData("""42""")]
    [InlineData("\"a string\"")]
    public void MalformedAdvancedJson_IsRejectedExplicitly(string advancedJson)
    {
        foreach (var protocol in new[]
        {
            ProviderProtocol.OpenAiResponses, ProviderProtocol.OpenAiCompatible,
            ProviderProtocol.AnthropicMessages, ProviderProtocol.GeminiGenerateContent
        })
        {
            var request = CreateRequest(protocol, advancedJson);
            var error = Assert.Throws<ModelCapabilityException>(
                () => ModelImageTestFixture.Build(request));
            Assert.Equal("invalid_model_parameters", error.Code);
        }
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
