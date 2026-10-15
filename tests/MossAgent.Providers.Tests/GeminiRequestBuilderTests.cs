using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.Gemini;
using MossAgent.Providers.ToolNames;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class GeminiRequestBuilderTests
{
    [Theory]
    [InlineData("minimal", "MINIMAL")]
    [InlineData("low", "LOW")]
    [InlineData("medium", "MEDIUM")]
    [InlineData("high", "HIGH")]
    public void Build_MapsEffortToGeminiThinkingLevel(string effort, string expectedLevel)
    {
        var request = CreateRequest(effort, "gemini-3-flash-preview");
        var toolNames = new ProviderToolNameMap([]);

        using var document = JsonDocument.Parse(GeminiRequestBuilder.Build(request, toolNames));
        var generationConfig = document.RootElement.GetProperty("generationConfig");

        Assert.Equal(expectedLevel, generationConfig
            .GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
        Assert.Equal(8_192, generationConfig.GetProperty("maxOutputTokens").GetInt32());
    }

    [Fact]
    public void Build_UsesThinkingBudgetInsteadOfThinkingLevelForGemini25Models()
    {
        var request = CreateRequest("high", "gemini-2.5-flash");
        var toolNames = new ProviderToolNameMap([]);

        using var document = JsonDocument.Parse(GeminiRequestBuilder.Build(request, toolNames));
        var thinking = document.RootElement.GetProperty("generationConfig")
            .GetProperty("thinkingConfig");

        Assert.Equal(7_168, thinking.GetProperty("thinkingBudget").GetInt32());
        Assert.False(thinking.TryGetProperty("thinkingLevel", out _));
    }

    [Theory]
    [InlineData("none", "gemini-2.5-flash", 8_192, 0)]
    [InlineData("minimal", "gemini-2.5-pro", 8_192, 128)]
    [InlineData("low", "gemini-2.5-flash", 8_192, 1_024)]
    [InlineData("medium", "gemini-2.5-pro", 8_192, 4_096)]
    [InlineData("xhigh", "gemini-2.5-pro", 65_536, 32_768)]
    [InlineData("xhigh", "gemini-2.5-flash", 65_536, 24_576)]
    public void Build_MapsGemini25EffortToBoundedThinkingBudget(
        string effort,
        string modelId,
        int maximumOutputTokens,
        int expectedBudget)
    {
        var request = CreateRequest(effort, modelId, maximumOutputTokens);
        var toolNames = new ProviderToolNameMap([]);

        using var document = JsonDocument.Parse(GeminiRequestBuilder.Build(request, toolNames));

        Assert.Equal(expectedBudget, document.RootElement.GetProperty("generationConfig")
            .GetProperty("thinkingConfig").GetProperty("thinkingBudget").GetInt32());
    }

    [Fact]
    public void Build_RejectsDisabledThinkingForGemini25Pro()
    {
        var request = CreateRequest("none", "gemini-2.5-pro");
        var toolNames = new ProviderToolNameMap([]);

        Assert.Throws<ArgumentException>(() => GeminiRequestBuilder.Build(request, toolNames));
    }

    private static ModelRequest CreateRequest(
        string effort,
        string modelId = "gemini-3.8-flash",
        int maximumOutputTokens = 8_192)
    {
        var providerId = Guid.NewGuid();
        var provider = new AiProvider(
            providerId, "Gemini", ProviderProtocol.GeminiGenerateContent,
            new Uri("https://example.test/v1beta/"), "test-key", null, true, true,
            new Dictionary<string, string>());
        var model = new ModelProfile(
            Guid.NewGuid(), providerId, modelId, "Gemini model",
            128_000, maximumOutputTokens, effort, null, null, false, false, true, true, "{}");
        return new ModelRequest(
            provider, model, [new ModelMessage(ModelRole.User, "hello")], []);
    }
}
