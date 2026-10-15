using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.Gemini;
using MossAgent.Providers.OpenAi;
using MossAgent.Providers.ToolNames;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ModelCapabilityRequestTests
{
    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, "custom-model")]
    [InlineData(ProviderProtocol.OpenAiCompatible, "custom-model")]
    [InlineData(ProviderProtocol.OpenAiResponses, "gpt-4.1")]
    public void CustomEffort_IsSentWithoutBeingReplacedByARecommendedValue(ProviderProtocol protocol, string modelId)
    {
        var request = Request(protocol, modelId, "vendor-extreme_2");
        var json = protocol == ProviderProtocol.OpenAiResponses
            ? OpenAiRequestBuilder.Build(request, new ProviderToolNameMap([]))
            : OpenAiCompatibleRequestBuilder.Build(request, new ProviderToolNameMap([]));
        using var document = JsonDocument.Parse(json);
        var effort = protocol == ProviderProtocol.OpenAiResponses
            ? document.RootElement.GetProperty("reasoning").GetProperty("effort")
            : document.RootElement.GetProperty("reasoning_effort");
        Assert.Equal("vendor-extreme_2", effort.GetString());
    }

    [Fact]
    public void NonReasoningResponsesModel_DoesNotSendAnUnsupportedEffortField()
    {
        var effort = ModelReasoningPolicy.DefaultEffort(ProviderProtocol.OpenAiResponses, "gpt-4.1");
        Assert.Equal("none", effort);
        using var document = JsonDocument.Parse(OpenAiRequestBuilder.Build(
            Request(ProviderProtocol.OpenAiResponses, "gpt-4.1", effort), new ProviderToolNameMap([])));
        Assert.False(document.RootElement.TryGetProperty("reasoning", out _));
    }

    [Fact]
    public void CatalogGeminiLevel_IsActuallyIncludedInTheRequest()
    {
        var effort = ModelReasoningPolicy.DefaultEffort(ProviderProtocol.GeminiGenerateContent, "gemini-4-argon");
        Assert.Equal("medium", effort);
        using var document = JsonDocument.Parse(GeminiRequestBuilder.Build(
            Request(ProviderProtocol.GeminiGenerateContent, "gemini-4-argon", effort), new ProviderToolNameMap([])));
        Assert.Equal("MEDIUM", document.RootElement.GetProperty("generationConfig")
            .GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
    }

    private static ModelRequest Request(ProviderProtocol protocol, string modelId, string effort)
    {
        var providerId = Guid.NewGuid();
        var provider = new AiProvider(providerId, "fixture", protocol, new Uri("https://example.test/"),
            "not-a-key", null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), providerId, modelId, modelId, 128000, 8192,
            effort, null, null, false, false, false, true, "{}");
        return new ModelRequest(provider, model, [new ModelMessage(ModelRole.User, "fixture")], []);
    }
}
