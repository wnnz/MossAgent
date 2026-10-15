using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelPickerSelectionTests
{
    [Fact]
    public async Task SelectionAvailability_BindingReadsDoNotAllocateAfterWarmup()
    {
        var (repository, _, _) = ModelPickerReloadTests.Profiles();
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var available = true;
        for (var iteration = 0; iteration < 1000; iteration++)
            available &= picker.CanUseSelectedModel;
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var iteration = 0; iteration < 10000; iteration++)
            available &= picker.CanUseSelectedModel;

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(available);
        Assert.Equal(0, allocated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectionEntries_ResolveTheCurrentRowAndRetainTheirEventSemantics(bool forAgent)
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        repository.Models[0] = model with { ContextLength = 64000 };
        await picker.ReloadAsync();
        var current = picker.SelectedItem;
        var events = new List<string>();
        ModelProfile? published = null;
        picker.ExplicitSelectionChanged += () => events.Add("explicit");
        picker.ModelSelected += (_, selected) => { published = selected; events.Add("model"); };
        picker.PreferenceChanged += () => events.Add("preference");
        picker.ShowModelListCommand.Execute(null);

        var accepted = forAgent
            ? picker.TrySelectForAgent(provider.Id, model.Id)
            : picker.TrySelect(provider.Id, model.Id);

        Assert.True(accepted);
        Assert.Same(current, picker.SelectedItem);
        Assert.Same(current?.Model, published);
        Assert.Equal(64000, published?.ContextLength);
        Assert.False(picker.IsModelListVisible);
        Assert.Equal(forAgent ? ["model"] : new[] { "explicit", "model", "preference" }, events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidProviderModelPairs_DoNotPublishOrChangeTheSelection(bool forAgent)
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        var second = provider with { Id = Guid.NewGuid(), IsDefault = false };
        var other = model with { Id = Guid.NewGuid(), ProviderId = second.Id };
        repository.Providers.Add(second);
        repository.Models.Add(other);
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var selected = picker.SelectedItem;
        var events = 0;
        picker.ModelSelected += (_, _) => events++;
        picker.ExplicitSelectionChanged += () => events++;
        picker.PreferenceChanged += () => events++;
        picker.ShowModelListCommand.Execute(null);

        var accepted = forAgent
            ? picker.TrySelectForAgent(provider.Id, other.Id)
            : picker.TrySelect(provider.Id, other.Id);

        Assert.False(accepted);
        Assert.Same(selected, picker.SelectedItem);
        Assert.True(picker.IsModelListVisible);
        Assert.Equal(0, events);
    }
}
