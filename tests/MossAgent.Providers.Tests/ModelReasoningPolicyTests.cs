using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ModelReasoningPolicyTests
{
    [Theory]
    [InlineData("gemini-3.8-flash", "low,medium,high")]
    [InlineData("gemini-3.7-flash-preview", "low,medium,high")]
    [InlineData(" GEMINI-3.1-PRO-PREVIEW ", "low,medium,high")]
    [InlineData("gemini-3-pro-preview-202610", "low,high")]
    [InlineData("gemini-3.1-flash-lite-image", "minimal,high")]
    [InlineData("gemini-3.6-flash", "minimal,low,medium,high")]
    [InlineData("gemini-3.5-flash", "minimal,low,medium,high")]
    [InlineData("gemini-3.5-flash-lite", "minimal,low,medium,high")]
    [InlineData("gemini-3-flash-preview", "minimal,low,medium,high")]
    [InlineData("gemini-3-future", "minimal,low,medium,high")]
    [InlineData("gemini-2.5-pro", "minimal,low,medium,high,xhigh,max")]
    [InlineData("gemini-2.5-flash", "none,minimal,low,medium,high,xhigh,max")]
    [InlineData("gemini-2.5-flash-lite-preview", "none,minimal,low,medium,high,xhigh,max")]
    [InlineData("custom-gemini-alias", "none")]
    [InlineData("gemini-2.0-flash", "none")]
    public void GeminiCapabilities_AreOrderedAndModelSpecific(string modelId, string expected)
    {
        var efforts = ModelReasoningPolicy.SupportedEfforts(ProviderProtocol.GeminiGenerateContent, modelId);
        Assert.Equal(expected.Split(','), efforts);
        Assert.All(efforts, effort => Assert.True(ModelReasoningPolicy.Supports(
            ProviderProtocol.GeminiGenerateContent, modelId, " " + effort.ToUpperInvariant() + " ")));
        Assert.True(ModelReasoningPolicy.Supports(ProviderProtocol.GeminiGenerateContent, modelId,
            ModelReasoningPolicy.DefaultEffort(ProviderProtocol.GeminiGenerateContent, modelId)));
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.OpenAiCompatible)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    public void NonGeminiProtocols_KeepExistingChoicesAndOffAlias(ProviderProtocol protocol)
    {
        Assert.Equal(8, ModelReasoningPolicy.SupportedEfforts(protocol, "gemini-3-pro-preview").Count);
        Assert.True(ModelReasoningPolicy.Supports(protocol, "gemini-3-pro-preview", " OFF "));
        Assert.True(ModelReasoningPolicy.Supports(protocol, "fixture", "ultra"));
        Assert.Equal(protocol != ProviderProtocol.AnthropicMessages,
            ModelReasoningPolicy.Supports(protocol, "fixture", "vendor-extreme"));
        Assert.False(ModelReasoningPolicy.Supports(protocol, "fixture", "invalid value"));
        Assert.False(ModelReasoningPolicy.Supports(protocol, "fixture", null));
    }

    [Theory]
    [InlineData("gemini-2.5-flash", true)]
    [InlineData("gemini-2.5-pro", false)]
    [InlineData("gemini-3.8-flash", false)]
    public void DisablingThinking_IsNotSubstitutedWithMinimal(string modelId, bool supported) =>
        Assert.Equal(supported, ModelReasoningPolicy.Supports(ProviderProtocol.GeminiGenerateContent, modelId, "off"));

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, "fixture")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "gemini-3.8-flash")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "gemini-3-future")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "gemini-3-pro")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "gemini-3.1-flash-lite-image")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "gemini-2.5-pro")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "gemini-2.5-flash")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "custom-alias")]
    public void SupportedEfforts_ReusesAnImmutableSnapshot(ProviderProtocol protocol, string modelId)
    {
        var efforts = ModelReasoningPolicy.SupportedEfforts(protocol, modelId);

        Assert.Same(efforts, ModelReasoningPolicy.SupportedEfforts(protocol, modelId));
        Assert.Same(efforts, ModelReasoningPolicy.SupportedEfforts(protocol, " " + modelId.ToUpperInvariant() + " "));
        var mutableView = Assert.IsAssignableFrom<IList<string>>(efforts);
        var original = efforts[0];
        Assert.Throws<NotSupportedException>(() => mutableView[0] = "unsupported");
        Assert.Equal(original, efforts[0]);
    }

    [Theory]
    [InlineData("gemini-3.8-flashlight", "minimal,low,medium,high")]
    [InlineData("GEMINI-3-PROXY", "minimal,low,medium,high")]
    [InlineData("gemini-3.1-flash-lite-images", "minimal,low,medium,high")]
    [InlineData("GEMINI-2.5-PROXY", "none")]
    [InlineData("gemini-2.5-flashlight", "none")]
    [InlineData("prefix-gemini-3.8-flash", "none")]
    public void FamilyMatching_RequiresAnExactNameOrHyphenSuffix(string modelId, string expected) =>
        Assert.Equal(expected.Split(','), ModelReasoningPolicy.SupportedEfforts(
            ProviderProtocol.GeminiGenerateContent, modelId));

    [Theory]
    [InlineData("gemini-3.8-flash", "medium")]
    [InlineData(" GEMINI-3-PRO-PREVIEW ", "high")]
    [InlineData("gemini-3.1-flash-lite-image", "high")]
    [InlineData("gemini-2.5-pro", "medium")]
    [InlineData("custom-alias", "none")]
    public void DefaultEffort_PrefersMediumOrTheHighestSupportedChoice(string modelId, string expected) =>
        Assert.Equal(expected, ModelReasoningPolicy.DefaultEffort(ProviderProtocol.GeminiGenerateContent, modelId));
}
