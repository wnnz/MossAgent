using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ConfigurationDraftCommitTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EditingDuringNewSave_RetryUpdatesTheCommittedIdentity(bool provider)
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        OpenDraft(provider, providers, models);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWriteAsync = token => pending.Task.WaitAsync(token);
        var command = provider ? providers.SaveCommand : models.SaveModelCommand;
        var save = command.ExecuteAsync(null);
        Assert.True(providers.Catalog.Mutations.IsRunning);
        ChangeName(provider, providers, models, "latest-draft");
        try { pending.SetResult(); await save; }
        finally { pending.TrySetResult(); await save; }

        Assert.True(provider ? providers.IsFormOpen : models.IsFormOpen);
        Assert.Equal("latest-draft", provider ? providers.Name : models.DisplayName);
        var committedId = provider ? repository.Store.Providers[1].Id : repository.Store.Models[1].Id;
        Assert.Equal(committedId, provider ? providers.SelectedItem?.Id : models.SelectedModel?.Id);
        if (provider) providers.ApiKey = string.Empty;
        repository.BeforeWriteAsync = null;

        await command.ExecuteAsync(null);

        Assert.False(provider ? providers.IsFormOpen : models.IsFormOpen);
        Assert.Equal(2, provider ? repository.Store.Providers.Count : repository.Store.Models.Count);
        Assert.Equal("latest-draft", provider ? repository.Store.Providers[1].Name : repository.Store.Models[1].DisplayName);
        Assert.Equal(committedId, provider ? repository.Store.Providers[1].Id : repository.Store.Models[1].Id);
        if (provider) Assert.Equal("fixture-draft-key", repository.Store.Providers[1].ApiKey);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CommittedDraftDeletedExternally_CannotBeResurrectedByRetry(bool provider)
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        OpenDraft(provider, providers, models);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWriteAsync = token => pending.Task.WaitAsync(token);
        var command = provider ? providers.SaveCommand : models.SaveModelCommand;
        var save = command.ExecuteAsync(null);
        ChangeName(provider, providers, models, "retained-draft");
        try { pending.SetResult(); await save; }
        finally { pending.TrySetResult(); await save; }
        repository.BeforeWriteAsync = null;
        if (provider) repository.Store.Providers.RemoveAt(1);
        else repository.Store.Models.RemoveAt(1);
        await providers.RefreshCommand.ExecuteAsync(null);

        Assert.True(provider ? providers.IsEditorTargetUnavailable : models.IsEditorTargetUnavailable);
        Assert.False(command.CanExecute(null));
        await command.ExecuteAsync(null);

        Assert.Single(repository.Writes);
        Assert.True(provider ? providers.IsFormOpen : models.IsFormOpen);
        Assert.Equal("retained-draft", provider ? providers.Name : models.DisplayName);
        Assert.Single(provider ? (System.Collections.IEnumerable)repository.Store.Providers : repository.Store.Models);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailureAfterCommit_RetryDoesNotCreateAnotherIdentity(bool provider)
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        OpenDraft(provider, providers, models);
        repository.AfterWriteAsync = _ => throw new IOException("fixture-draft-key");
        var command = provider ? providers.SaveCommand : models.SaveModelCommand;

        await command.ExecuteAsync(null);

        Assert.True(provider ? providers.IsFormOpen : models.IsFormOpen);
        Assert.True(provider ? providers.HasValidationError : models.HasStatusError);
        Assert.DoesNotContain("fixture-draft-key", provider ? providers.Status : models.Status);
        var committedId = provider ? repository.Store.Providers[1].Id : repository.Store.Models[1].Id;
        repository.AfterWriteAsync = null;
        await command.ExecuteAsync(null);

        Assert.False(provider ? providers.IsFormOpen : models.IsFormOpen);
        Assert.Equal(2, provider ? repository.Store.Providers.Count : repository.Store.Models.Count);
        Assert.Equal(committedId, provider ? repository.Store.Providers[1].Id : repository.Store.Models[1].Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NewDraftOpenedDuringSave_KeepsItsOwnIdentity(bool provider)
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        OpenDraft(provider, providers, models);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWriteAsync = token => pending.Task.WaitAsync(token);
        var command = provider ? providers.SaveCommand : models.SaveModelCommand;
        var save = command.ExecuteAsync(null);
        OpenDraft(provider, providers, models);
        ChangeName(provider, providers, models, "independent-draft");
        try { pending.SetResult(); await save; }
        finally { pending.TrySetResult(); await save; }
        var firstId = provider ? repository.Store.Providers[1].Id : repository.Store.Models[1].Id;
        Assert.Null(provider ? providers.SelectedItem?.Id : models.SelectedModel?.Id);
        repository.BeforeWriteAsync = null;

        await command.ExecuteAsync(null);

        Assert.Equal(3, provider ? repository.Store.Providers.Count : repository.Store.Models.Count);
        Assert.NotEqual(firstId, provider ? repository.Store.Providers[2].Id : repository.Store.Models[2].Id);
        Assert.Equal("independent-draft", provider ? repository.Store.Providers[2].Name : repository.Store.Models[2].DisplayName);
    }

    private static void OpenDraft(bool provider, ProviderSettingsViewModel providers, ModelSettingsViewModel models)
    {
        if (provider)
        {
            providers.OpenCreateFormCommand.Execute(null);
            providers.Name = "initial-draft";
            providers.ApiKey = "fixture-draft-key";
        }
        else
        {
            models.OpenCreateFormCommand.Execute(null);
            models.ModelId = "fixture-draft-model";
            models.DisplayName = "initial-draft";
            models.ReasoningEffort = "off";
        }
    }

    private static void ChangeName(bool provider, ProviderSettingsViewModel providers,
        ModelSettingsViewModel models, string name)
    {
        if (provider) providers.Name = name;
        else models.DisplayName = name;
    }
}
