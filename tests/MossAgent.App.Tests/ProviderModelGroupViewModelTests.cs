using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProviderModelGroupViewModelTests
{
    [Fact]
    public void ModelCount_TracksModelsWhileProviderIsCollapsedOrExpanded()
    {
        var provider = new AiProvider(
            Guid.NewGuid(), "Gemini", ProviderProtocol.GeminiGenerateContent,
            new Uri("https://example.test/v1beta/"), "fixture-key", null,
            false, true, new Dictionary<string, string>());
        var group = new ProviderModelGroupViewModel(provider);

        Assert.Equal(0, group.ModelCount);
        Assert.True(group.HasNoModels);
        group.Models.Add(new ModelProfile(
            Guid.NewGuid(), provider.Id, "gemini-3.8-flash", "Gemini 3.8 Flash",
            128_000, 8_192, "high", null, null, false, false, true, true, "{}"));

        Assert.Equal(1, group.ModelCount);
        Assert.False(group.HasNoModels);
        Assert.False(group.IsExpanded);
        Assert.Equal("展开 Gemini 的模型", group.ExpansionActionLabel);
        group.IsExpanded = true;
        Assert.True(group.IsExpanded);
        Assert.Equal("收起 Gemini 的模型", group.ExpansionActionLabel);
    }
}
