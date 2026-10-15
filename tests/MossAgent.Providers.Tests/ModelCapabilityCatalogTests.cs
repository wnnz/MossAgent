using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ModelCapabilityCatalogTests
{
    [Fact]
    public void BundledSnapshot_HasProvenanceAndValidImmutableEntries()
    {
        var data = ModelCapabilityCatalog.Data;
        Assert.Equal(1, data.SchemaVersion);
        Assert.Equal("https://models.dev/api.json", data.Source);
        Assert.Matches("^[a-f0-9]{64}$", data.SourceSha256);
        Assert.True(data.RetrievedAt > DateTimeOffset.Parse("2026-01-01"));
        Assert.InRange(data.Models.Length, 30, 1000);
        Assert.Equal(data.Models.Length, data.Models.Select(item => item.Provider + "/" + item.ModelId).Distinct().Count());
        Assert.All(data.Models, model =>
        {
            Assert.InRange(model.ContextLength, 1024, 4_000_000);
            Assert.InRange(model.MaximumOutputTokens, 256, 4_000_000);
            Assert.Same(model, ModelCapabilityCatalog.Find(ProviderProtocol.OpenAiCompatible, model.Provider + "/" + model.ModelId));
            Assert.All(model.ReasoningEfforts, effort => Assert.Matches("^[a-z][a-z0-9_-]{0,63}$", effort));
        });
    }

    [Theory]
    [InlineData("gpt-5", 400000, 128000, true, false, "minimal,low,medium,high")]
    [InlineData("claude-opus-4-6", 1000000, 128000, true, true, "low,medium,high,max")]
    [InlineData("gemini-3.8-flash", 1048576, 65536, true, true, "low,medium,high")]
    public void CommonModels_HaveSourceBackedDefaults(string id, int context, int output, bool images, bool files, string efforts)
    {
        var model = Assert.IsType<ModelCapabilities>(ModelCapabilityCatalog.Find(ProviderProtocol.OpenAiCompatible, id));
        Assert.Equal(context, model.ContextLength);
        Assert.Equal(output, model.MaximumOutputTokens);
        Assert.Equal(images, model.SupportsImages);
        Assert.Equal(files, model.SupportsFiles);
        Assert.Equal(efforts.Split(','), model.ReasoningEfforts);
        Assert.Same(model, ModelCapabilityCatalog.Find(ProviderProtocol.OpenAiCompatible, " " + id.ToUpperInvariant() + " "));
    }

    [Theory]
    [InlineData("claude-opus-4-6-thinking")]
    [InlineData("gpt-5-unknown-suffix")]
    [InlineData("prefix-gemini-3.8-flash")]
    [InlineData("untrusted/gpt-5")]
    [InlineData("unknown-vision-thinking")]
    public void UnknownAliases_AreNotGuessed(string id) =>
        Assert.Null(ModelCapabilityCatalog.Find(ProviderProtocol.OpenAiCompatible, id));

    [Fact]
    public void NativeProtocols_DoNotBorrowOtherProvidersCapabilities()
    {
        Assert.Null(ModelCapabilityCatalog.Find(ProviderProtocol.GeminiGenerateContent, "gpt-5"));
        Assert.Null(ModelCapabilityCatalog.Find(ProviderProtocol.AnthropicMessages, "gemini-3.8-flash"));
        Assert.NotNull(ModelCapabilityCatalog.Find(ProviderProtocol.GeminiGenerateContent, "models/gemini-3.8-flash"));
    }

    [Theory]
    [InlineData("gpt-5", "minimal,low,medium,high")]
    [InlineData("claude-opus-4-6", "low,medium,high,max")]
    [InlineData("gpt-4.1", "none")]
    public void RecommendedEfforts_ReuseAnImmutableModelSpecificSnapshot(string id, string expected)
    {
        var efforts = ModelReasoningPolicy.SupportedEfforts(ProviderProtocol.OpenAiCompatible, id);
        Assert.Equal(expected.Split(','), efforts);
        Assert.Same(efforts, ModelReasoningPolicy.SupportedEfforts(ProviderProtocol.OpenAiCompatible, id));
        var list = Assert.IsAssignableFrom<IList<string>>(efforts);
        Assert.Throws<NotSupportedException>(() => list[0] = "changed");
    }

    [Theory]
    [InlineData("custom-level_2", true)]
    [InlineData(" VENDOR-EXTREME ", true)]
    [InlineData("", false)]
    [InlineData("bad value", false)]
    [InlineData("bad\nvalue", false)]
    [InlineData("<script>", false)]
    [InlineData("123", false)]
    public void CompatibleEffortInput_AllowsBoundedTokensNotArbitraryContent(string effort, bool valid)
    {
        Assert.Equal(valid, ModelReasoningPolicy.Supports(ProviderProtocol.OpenAiCompatible, "custom", effort));
        Assert.Equal(valid, ModelReasoningPolicy.Supports(ProviderProtocol.OpenAiResponses, "gpt-5", effort));
        Assert.False(ModelReasoningPolicy.Supports(ProviderProtocol.OpenAiResponses, "custom", new string('a', 65)));
    }

    [Fact]
    public void NativeAnthropic_StillSupportsManualBudgetsAndDisablingThinking()
    {
        Assert.Contains("high", ModelReasoningPolicy.SupportedEfforts(ProviderProtocol.AnthropicMessages, "claude-haiku-4-5"));
        Assert.True(ModelReasoningPolicy.Supports(ProviderProtocol.AnthropicMessages, "claude-opus-4-6", "off"));
        Assert.False(ModelReasoningPolicy.Supports(ProviderProtocol.AnthropicMessages, "claude-opus-4-6", "vendor-custom"));
    }
}
