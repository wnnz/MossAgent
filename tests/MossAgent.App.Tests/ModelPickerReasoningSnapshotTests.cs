using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelPickerReasoningSnapshotTests
{
    [Fact]
    public async Task BindingReadsAndReasoningChanges_ReuseAnImmutableSnapshot()
    {
        var (repository, _, _) = ModelPickerReloadTests.Profiles();
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var steps = picker.ReasoningSteps;

        picker.ReasoningSliderLabel = "Ultra";
        picker.SearchQuery = "fixture";
        picker.ShowModelListCommand.Execute(null);

        Assert.Same(steps, picker.ReasoningSteps);
        Assert.Same(steps, picker.ReasoningSteps);
        Assert.Equal("Ultra", picker.ReasoningSliderLabel);
        Assert.Equal("ultra", picker.EffectiveReasoningEffort);
        var list = Assert.IsAssignableFrom<IList<string>>(steps);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = "changed");
    }

    [Fact]
    public async Task SelectedItemObservers_SeeTheUpdatedStepsImmediately()
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        var gemini = provider with { Id = Guid.NewGuid(), Protocol = ProviderProtocol.GeminiGenerateContent, IsDefault = false };
        var flash = model with { Id = Guid.NewGuid(), ProviderId = gemini.Id, ModelId = "gemini-3.8-flash" };
        repository.Providers.Add(gemini);
        repository.Models.Add(flash);
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var observed = new List<IReadOnlyList<string>>();
        picker.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(picker.SelectedItem)) observed.Add(picker.ReasoningSteps);
        };

        Assert.True(picker.TrySelect(gemini.Id, flash.Id));

        Assert.Equal(["低", "中", "高"], Assert.Single(observed));
        Assert.Same(picker.ReasoningSteps, observed[0]);
    }

    [Theory]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "gemini-3.8-flash", "低,中,高")]
    [InlineData(ProviderProtocol.GeminiGenerateContent, "gemini-3-pro-preview", "低,高")]
    [InlineData(ProviderProtocol.OpenAiResponses, "updated-model", "无,最低,低,中,高,最高,Max,Ultra")]
    public async Task ModelReload_RebuildsSupportedStepsWithoutClampingTheDraft(ProviderProtocol protocol, string modelId, string labels)
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        picker.ReasoningSliderLabel = "Ultra";
        var original = picker.ReasoningSteps;
        repository.Providers[0] = provider with { Protocol = protocol };
        repository.Models[0] = model with { ModelId = modelId };

        await picker.ReloadAsync();

        Assert.NotSame(original, picker.ReasoningSteps);
        Assert.Equal(labels.Split(','), picker.ReasoningSteps);
        Assert.Equal("Ultra", picker.SelectedReasoning);
        Assert.Equal("Ultra", picker.ReasoningSliderLabel);
        Assert.Equal(protocol == ProviderProtocol.GeminiGenerateContent, picker.HasReasoningError);
        Assert.Equal(protocol != ProviderProtocol.GeminiGenerateContent, picker.CanSendWithSelectedModel);
    }

    [Fact]
    public async Task RemovedSelection_RestoresNoModelStepsWithoutChangingTheReasoningDraft()
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        repository.Providers[0] = provider with { Protocol = ProviderProtocol.GeminiGenerateContent };
        repository.Models[0] = model with { ModelId = "gemini-3-pro-preview" };
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        picker.ReasoningSliderLabel = "高";
        var selectedSteps = picker.ReasoningSteps;
        Assert.Equal(["低", "高"], selectedSteps);
        repository.Models[0] = repository.Models[0] with { IsEnabled = false };

        await picker.ReloadAsync();

        Assert.Null(picker.SelectedItem);
        Assert.NotSame(selectedSteps, picker.ReasoningSteps);
        Assert.Equal(8, picker.ReasoningSteps.Count);
        Assert.Equal("高", picker.SelectedReasoning);
        Assert.Equal(string.Empty, picker.ReasoningSliderLabel);
        Assert.False(picker.CanSendWithSelectedModel);
    }
}
