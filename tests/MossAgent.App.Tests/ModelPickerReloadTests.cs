using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelPickerReloadTests
{
    [Theory]
    [InlineData("disabledModel")]
    [InlineData("removedModel")]
    [InlineData("disabledProvider")]
    public async Task UnavailableSelection_IsNotSilentlyReplacedAndRestoresTheSameIdentity(string state)
    {
        var (repository, provider, model) = Profiles();
        var secondProvider = provider with { Id = Guid.NewGuid(), Name = "alternative", IsDefault = false };
        var alternative = model with { Id = Guid.NewGuid(), ProviderId = secondProvider.Id, ModelId = "alternative" };
        repository.Providers.Add(secondProvider);
        repository.Models.Add(alternative);
        var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        picker.SelectedReasoning = "高";
        switch (state)
        {
            case "disabledModel": repository.Models[0] = model with { IsEnabled = false }; break;
            case "removedModel": repository.Models.Remove(model); break;
            case "disabledProvider": repository.Providers[0] = provider with { IsEnabled = false }; break;
        }

        await picker.ReloadAsync();
        await picker.ReloadAsync();

        Assert.Null(picker.SelectedItem);
        Assert.Equal("高", picker.SelectedReasoning);
        repository.Providers[0] = provider;
        repository.Models.RemoveAll(item => item.Id == model.Id);
        repository.Models.Add(model);
        await picker.ReloadAsync();
        Assert.Equal(model.Id, picker.SelectedItem?.Model.Id);
        Assert.Equal("high", picker.EffectiveReasoningEffort);
    }

    [Fact]
    public async Task EarlierReload_CannotAppendOrSelectItsOlderSnapshot()
    {
        var (repository, _, model) = Profiles();
        var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var delayed = new TaskCompletionSource<IReadOnlyList<ModelProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ModelLoader = _ => delayed.Task;
        var earlier = picker.ReloadAsync();
        var newer = model with { DisplayName = "newer", ContextLength = 32000 };
        repository.ModelLoader = _ => Task.FromResult<IReadOnlyList<ModelProfile>>([newer]);
        await picker.ReloadAsync();
        delayed.SetResult([model]);
        await earlier;

        Assert.Equal(newer, picker.SelectedItem?.Model);
        Assert.Equal(newer, Assert.Single(Assert.Single(picker.FilteredGroups).Items).Model);
    }

    [Fact]
    public async Task FailedReload_DoesNotThrowOrPublishAPartialCatalog()
    {
        var (repository, provider, model) = Profiles();
        var secondProvider = provider with { Id = Guid.NewGuid(), Name = "second" };
        var secondModel = model with { Id = Guid.NewGuid(), ProviderId = secondProvider.Id };
        repository.Providers.Add(secondProvider);
        repository.Models.Add(secondModel);
        var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var snapshot = picker.FilteredGroups.ToArray();
        repository.ModelLoader = id => id == provider.Id
            ? Task.FromResult<IReadOnlyList<ModelProfile>>([model with { DisplayName = "partial" }])
            : Task.FromException<IReadOnlyList<ModelProfile>>(new InvalidOperationException("fixture-secret-error"));

        Assert.Null(await Record.ExceptionAsync(picker.ReloadAsync));

        Assert.Equal(snapshot, picker.FilteredGroups);
        Assert.Equal(model, picker.SelectedItem?.Model);
    }

    [Fact]
    public async Task EarlierError_CannotInvalidateANewerSuccessfulReload()
    {
        var (repository, _, model) = Profiles();
        var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var delayed = new TaskCompletionSource<IReadOnlyList<ModelProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ModelLoader = _ => delayed.Task;
        var earlier = picker.ReloadAsync();
        var newer = model with { DisplayName = "newer" };
        repository.ModelLoader = _ => Task.FromResult<IReadOnlyList<ModelProfile>>([newer]);
        await picker.ReloadAsync();
        delayed.SetException(new InvalidOperationException("fixture-secret-error"));

        Assert.Null(await Record.ExceptionAsync(() => earlier));
        Assert.Equal(newer, picker.SelectedItem?.Model);
    }

    [Fact]
    public async Task StaleMenuCommand_CannotReselectADisabledModel()
    {
        var (repository, _, model) = Profiles();
        repository.Models.Add(model with { Id = Guid.NewGuid(), ModelId = "alternative", IsDefault = false });
        var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var stale = picker.SelectedItem;
        repository.Models[0] = model with { IsEnabled = false };
        await picker.ReloadAsync();
        var afterReload = picker.SelectedItem;

        picker.SelectItemCommand.Execute(stale);

        Assert.Same(afterReload, picker.SelectedItem);
        Assert.False(picker.SelectItemCommand.CanExecute(stale));
    }

    [Fact]
    public async Task MenuCommand_UsesCurrentParametersInsteadOfThePassedStaleRecord()
    {
        var (repository, _, model) = Profiles();
        var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var stale = picker.SelectedItem;
        var updated = model with { DisplayName = "updated", ContextLength = 64000, MaximumOutputTokens = 4000 };
        repository.Models[0] = updated;
        await picker.ReloadAsync();
        ModelProfile? published = null;
        picker.ModelSelected += (_, selected) => published = selected;

        picker.SelectItemCommand.Execute(stale);

        Assert.Equal(updated, picker.SelectedItem?.Model);
        Assert.Equal(updated, published);
    }

    [Fact]
    public async Task ModelsOutsideTheLoadedCatalog_CannotBeSelected()
    {
        var (repository, provider, model) = Profiles();
        var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var unknown = new ModelPickerItemViewModel(provider, model with { Id = Guid.NewGuid() });

        Assert.False(picker.SelectItemCommand.CanExecute(unknown));
        picker.SelectItemCommand.Execute(unknown);
        Assert.Equal(model.Id, picker.SelectedItem?.Model.Id);
    }

    internal static (ModelPickerProbeRepository Repository, AiProvider Provider, ModelProfile Model) Profiles()
    {
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://fixture.example.test/v1"), "fixture-key", null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "fixture-model", "Fixture model",
            16000, 2000, "medium", null, null, false, false, true, true, "{}");
        var repository = new ModelPickerProbeRepository();
        repository.Providers.Add(provider);
        repository.Models.Add(model);
        return (repository, provider, model);
    }
}
