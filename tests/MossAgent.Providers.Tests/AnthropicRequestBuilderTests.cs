using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Providers.Anthropic;
using MossAgent.Providers.ToolNames;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class AnthropicRequestBuilderTests
{
    [Fact]
    public void Build_UsesAdaptiveThinkingForClaude46AndLater()
    {
        using var document = Build("claude-sonnet-4-6", "high");
        var root = document.RootElement;

        Assert.Equal("adaptive", root.GetProperty("thinking").GetProperty("type").GetString());
        Assert.Equal("high", root.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.False(root.GetProperty("thinking").TryGetProperty("budget_tokens", out _));
    }

    [Fact]
    public void Build_UsesManualThinkingBudgetForClaude45()
    {
        using var document = Build("claude-sonnet-4-5-20250929", "medium");
        var thinking = document.RootElement.GetProperty("thinking");

        Assert.Equal("enabled", thinking.GetProperty("type").GetString());
        Assert.Equal(4_096, thinking.GetProperty("budget_tokens").GetInt32());
        Assert.False(document.RootElement.TryGetProperty("output_config", out _));
    }

    [Fact]
    public void Build_UsesEffortAlongsideBudgetForOpus45()
    {
        using var document = Build("claude-opus-4-5", "high", 16_384);
        var root = document.RootElement;

        Assert.Equal(8_192, root.GetProperty("thinking").GetProperty("budget_tokens").GetInt32());
        Assert.Equal("high", root.GetProperty("output_config").GetProperty("effort").GetString());
    }

    [Theory]
    [InlineData("none")]
    [InlineData("off")]
    public void Build_DoesNotSetThinkingForDisabledEffort(string effort)
    {
        using var document = Build("claude-sonnet-4-6", effort);

        Assert.False(document.RootElement.TryGetProperty("thinking", out _));
        Assert.False(document.RootElement.TryGetProperty("output_config", out _));
    }

    [Fact]
    public void Build_DoesNotSetThinkingForUnsupportedModelGeneration()
    {
        using var document = Build("claude-3-5-sonnet-20241022", "high");

        Assert.False(document.RootElement.TryGetProperty("thinking", out _));
    }

    [Fact]
    public void Build_LeavesOutputRoomWhenCappingManualThinkingBudget()
    {
        using var document = Build("claude-sonnet-4-5", "high", 2_048);

        Assert.Equal(1_024, document.RootElement.GetProperty("thinking")
            .GetProperty("budget_tokens").GetInt32());
    }

    [Fact]
    public void Build_SkipsManualThinkingWhenOutputBudgetCannotFitMinimum()
    {
        using var document = Build("claude-sonnet-4-5", "high", 1_024);

        Assert.False(document.RootElement.TryGetProperty("thinking", out _));
    }

    [Fact]
    public void Build_SkipsManualThinkingWhenResponseReserveCannotBeMet()
    {
        using var document = Build("claude-sonnet-4-5", "high", 1_536);

        Assert.False(document.RootElement.TryGetProperty("thinking", out _));
    }

    [Theory]
    [InlineData("claude-sonnet-4-6", "xhigh")]
    [InlineData("claude-sonnet-4-6", "ultra")]
    [InlineData("claude-opus-4-5", "xhigh")]
    [InlineData("claude-sonnet-4-6", "default")]
    public void Build_RejectsEffortOutsideModelCapability(string modelId, string effort)
    {
        var error = Assert.Throws<ModelCapabilityException>(
            () => Build(modelId, effort));

        Assert.Equal("unsupported_reasoning_effort", error.Code);
    }

    [Fact]
    public void Build_MapsXHighToHighForCatalogUnknownDatedVariants()
    {
        // The dated variant is absent from the bundled catalog, so the generic
        // effort set admits xhigh and the conservative family map lowers it.
        using var document = Build("claude-sonnet-4-6-20250929", "xhigh");

        Assert.Equal("high", document.RootElement.GetProperty("output_config")
            .GetProperty("effort").GetString());
    }

    [Fact]
    public void Build_PreservesXHighEffortForSupportedModels()
    {
        using var document = Build("claude-opus-4-7", "xhigh");

        Assert.Equal("xhigh", document.RootElement.GetProperty("output_config")
            .GetProperty("effort").GetString());
    }

    private static JsonDocument Build(
        string modelId,
        string effort,
        int maximumOutputTokens = 8_192)
    {
        var providerId = Guid.NewGuid();
        var provider = new AiProvider(
            providerId, "Anthropic", ProviderProtocol.AnthropicMessages,
            new Uri("https://example.test/v1/"), "test-key", null, true, true,
            new Dictionary<string, string>());
        var model = new ModelProfile(
            Guid.NewGuid(), providerId, modelId, modelId,
            128_000, maximumOutputTokens, effort, null, null, false, false, true, true, "{}");
        var request = new ModelRequest(
            provider, model, [new ModelMessage(ModelRole.User, "hello")], []);
        return JsonDocument.Parse(AnthropicRequestBuilder.Build(request, new ProviderToolNameMap([])));
    }
}
