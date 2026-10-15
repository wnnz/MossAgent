using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelPickerAvailabilityTests
{
    [Fact]
    public async Task Reload_ExposesLoadingStateAndBlocksStaleSelectionUntilComplete()
    {
        var (repository, _, model) = ModelPickerReloadTests.Profiles();
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var selected = picker.SelectedItem;
        picker.SelectedReasoning = "高";
        var delayed = new TaskCompletionSource<IReadOnlyList<ModelProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ModelLoader = _ => delayed.Task;
        var pending = picker.ReloadAsync();

        Assert.True(picker.IsLoading);
        Assert.False(picker.CanUseSelectedModel);
        Assert.False(picker.CanChooseModel);
        Assert.False(picker.RefreshCommand.CanExecute(null));
        Assert.False(picker.SelectItemCommand.CanExecute(selected));
        Assert.True(picker.HasCatalogNotice);
        Assert.Contains("刷新", picker.CatalogNotice, StringComparison.Ordinal);
        Assert.Same(selected, picker.SelectedItem);
        picker.SelectItemCommand.Execute(selected);
        delayed.SetResult([model with { ContextLength = 64000 }]);
        await pending;

        Assert.False(picker.IsLoading);
        Assert.True(picker.CanUseSelectedModel);
        Assert.Equal(64000, picker.SelectedItem?.Model.ContextLength);
        Assert.Equal("高", picker.SelectedReasoning);
        Assert.False(picker.HasCatalogNotice);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadFailures_KeepTheSnapshotButBlockUseAndHideExceptionDetails(bool providers)
    {
        var (repository, _, _) = ModelPickerReloadTests.Profiles();
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var selected = picker.SelectedItem;
        picker.SearchQuery = "fixture";
        picker.SelectedReasoning = "高";
        if (providers) repository.ProviderLoader = () => Task.FromException<IReadOnlyList<AiProvider>>(
            new InvalidOperationException("fixture-secret-error"));
        else repository.ModelLoader = _ => Task.FromException<IReadOnlyList<ModelProfile>>(
            new InvalidOperationException("fixture-secret-error"));

        await picker.ReloadAsync();

        Assert.Same(selected, picker.SelectedItem);
        Assert.True(picker.HasModels);
        Assert.True(picker.HasLoadError);
        Assert.False(picker.CanUseSelectedModel);
        Assert.False(picker.IsLoading);
        Assert.True(picker.IsModelListVisible);
        Assert.True(picker.RefreshCommand.CanExecute(null));
        Assert.Equal("高", picker.SelectedReasoning);
        Assert.Equal("fixture", picker.SearchQuery);
        Assert.DoesNotContain("fixture-secret-error", picker.CatalogNotice, StringComparison.Ordinal);
        Assert.Equal("模型列表不可用", picker.CurrentDisplayName);
        repository.ProviderLoader = null;
        repository.ModelLoader = null;
        await picker.ReloadAsync();
        Assert.True(picker.CanUseSelectedModel);
        Assert.False(picker.HasLoadError);
    }

    [Fact]
    public async Task ExplicitReplacement_ChangesTheRememberedIdentityAndDoesNotRevert()
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        var alternative = model with { Id = Guid.NewGuid(), ModelId = "alternative", IsDefault = false };
        repository.Models.Add(alternative);
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        repository.Models.Remove(model);
        await picker.ReloadAsync();
        Assert.True(picker.IsSelectedUnavailable);
        Assert.False(picker.CanUseSelectedModel);
        Assert.Equal("所选模型不可用", picker.CurrentDisplayName);

        Assert.True(picker.TrySelect(provider.Id, alternative.Id));
        repository.Models.Add(model);
        await picker.ReloadAsync();

        Assert.Equal(alternative.Id, picker.SelectedItem?.Model.Id);
        Assert.True(picker.CanUseSelectedModel);
        Assert.False(picker.IsSelectedUnavailable);
    }

    [Fact]
    public async Task Disposal_RejectsLateSnapshotsAndFurtherReads()
    {
        var (repository, _, model) = ModelPickerReloadTests.Profiles();
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var snapshot = picker.FilteredGroups.ToArray();
        var delayed = new TaskCompletionSource<IReadOnlyList<ModelProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ModelLoader = _ => delayed.Task;
        var pending = picker.ReloadAsync();
        var events = 0;
        picker.ModelSelected += (_, _) => events++;

        picker.Dispose();
        delayed.SetResult([model with { DisplayName = "late" }]);
        await pending;
        repository.ProviderLoader = () => throw new InvalidOperationException("unexpected read after disposal");
        await picker.ReloadAsync();

        Assert.Equal(0, events);
        Assert.Equal(snapshot, picker.FilteredGroups);
        Assert.False(picker.CanUseSelectedModel);
        Assert.False(picker.IsLoading);
        Assert.False(picker.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public async Task AModelFromAnotherProvider_IsNotOfferedOrSelected()
    {
        var (repository, _, model) = ModelPickerReloadTests.Profiles();
        repository.ModelLoader = _ => Task.FromResult<IReadOnlyList<ModelProfile>>(
            [model with { ProviderId = Guid.NewGuid() }]);
        using var picker = new ModelPickerViewModel(repository);

        await picker.ReloadAsync();

        Assert.True(picker.HasNoModels);
        Assert.Null(picker.SelectedItem);
        Assert.False(picker.CanUseSelectedModel);
    }
}
