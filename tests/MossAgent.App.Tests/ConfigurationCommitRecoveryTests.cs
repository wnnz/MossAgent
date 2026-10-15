using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ConfigurationCommitRecoveryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailureAfterCommit_ReconcilesIdentityWithoutDiscardingChangedFields(bool provider)
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        OpenDraft(provider, providers, models);
        repository.AfterWriteAsync = _ =>
        {
            if (provider) { providers.Name = "latest-provider"; providers.ApiKey = string.Empty; }
            else models.DisplayName = "latest-model";
            throw new IOException("fixture-recovery-secret");
        };
        var command = provider ? providers.SaveCommand : models.SaveModelCommand;

        await command.ExecuteAsync(null);

        var committedId = provider ? repository.Store.Providers[1].Id : repository.Store.Models[1].Id;
        Assert.Equal(committedId, provider ? providers.SelectedItem?.Id : models.SelectedModel?.Id);
        Assert.True(provider ? providers.IsFormOpen : models.IsFormOpen);
        Assert.Equal(provider ? "latest-provider" : "latest-model", provider ? providers.Name : models.DisplayName);
        Assert.DoesNotContain("fixture-recovery-secret", provider ? providers.Status : models.Status);
        if (provider) Assert.Equal(string.Empty, providers.ApiKey);
        repository.AfterWriteAsync = null;

        await command.ExecuteAsync(null);

        Assert.False(provider ? providers.IsFormOpen : models.IsFormOpen);
        Assert.Equal(2, provider ? repository.Store.Providers.Count : repository.Store.Models.Count);
        Assert.Equal(committedId, provider ? repository.Store.Providers[1].Id : repository.Store.Models[1].Id);
        if (provider) Assert.Equal("fixture-recovery-key", repository.Store.Providers[1].ApiKey);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailureAfterCommit_ReadFailureDisablesRetryUntilRefreshRestoresIdentity(bool provider)
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        OpenDraft(provider, providers, models);
        repository.AfterWriteAsync = _ =>
        {
            repository.Store.ProviderLoader = () => throw new IOException("fixture-recovery-read-secret");
            throw new IOException("fixture-recovery-secret");
        };
        var command = provider ? providers.SaveCommand : models.SaveModelCommand;

        await command.ExecuteAsync(null);

        Assert.True(providers.Catalog.HasLoadError);
        Assert.False(command.CanExecute(null));
        Assert.True(provider ? providers.HasValidationError : models.HasStatusError);
        Assert.DoesNotContain("fixture-recovery", providers.Catalog.Notice);
        await command.ExecuteAsync(null);
        Assert.Single(repository.Writes);
        repository.Store.ProviderLoader = null;
        repository.AfterWriteAsync = null;

        await providers.RefreshCommand.ExecuteAsync(null);

        var committedId = provider ? repository.Store.Providers[1].Id : repository.Store.Models[1].Id;
        Assert.Equal(committedId, provider ? providers.SelectedItem?.Id : models.SelectedModel?.Id);
        Assert.True(command.CanExecute(null));
        if (provider) providers.ApiKey = string.Empty;
        await command.ExecuteAsync(null);
        Assert.Equal(2, provider ? repository.Store.Providers.Count : repository.Store.Models.Count);
        if (provider) Assert.Equal("fixture-recovery-key", repository.Store.Providers[1].ApiKey);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailureBeforeCommit_KeepsNewDraftAndAllowsExplicitRetry(bool provider)
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        OpenDraft(provider, providers, models);
        repository.FailingOperation = provider ? "save-provider" : "save-model";
        var command = provider ? providers.SaveCommand : models.SaveModelCommand;

        await command.ExecuteAsync(null);

        Assert.Null(provider ? providers.SelectedItem?.Id : models.SelectedModel?.Id);
        Assert.False(providers.Catalog.HasLoadError);
        Assert.True(command.CanExecute(null));
        Assert.True(provider ? providers.HasValidationError : models.HasStatusError);
        Assert.Single(provider ? (System.Collections.IEnumerable)repository.Store.Providers : repository.Store.Models);
        repository.FailingOperation = null;
        await command.ExecuteAsync(null);
        Assert.Equal(2, provider ? repository.Store.Providers.Count : repository.Store.Models.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReconciledCommittedTarget_DeletionCannotBeResurrectedByRetry(bool provider)
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        OpenDraft(provider, providers, models);
        repository.AfterWriteAsync = _ => throw new IOException("fixture-recovery-secret");
        var command = provider ? providers.SaveCommand : models.SaveModelCommand;
        await command.ExecuteAsync(null);
        repository.AfterWriteAsync = null;
        if (provider) repository.Store.Providers.RemoveAt(1);
        else repository.Store.Models.RemoveAt(1);

        await providers.RefreshCommand.ExecuteAsync(null);

        Assert.True(provider ? providers.IsEditorTargetUnavailable : models.IsEditorTargetUnavailable);
        Assert.False(command.CanExecute(null));
        await command.ExecuteAsync(null);
        Assert.Single(repository.Writes);
        Assert.True(provider ? providers.IsFormOpen : models.IsFormOpen);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailureAfterCommit_DoesNotAttachOlderIdentityToAnotherNewDraft(bool provider)
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        OpenDraft(provider, providers, models);
        repository.AfterWriteAsync = _ =>
        {
            OpenDraft(provider, providers, models);
            if (!provider) models.ModelId = "independent-recovery-model";
            throw new IOException("fixture-recovery-secret");
        };
        var command = provider ? providers.SaveCommand : models.SaveModelCommand;
        await command.ExecuteAsync(null);
        var previousId = provider ? repository.Store.Providers[1].Id : repository.Store.Models[1].Id;

        Assert.Null(provider ? providers.SelectedItem?.Id : models.SelectedModel?.Id);
        Assert.False(provider ? providers.HasValidationError : models.HasStatusError);
        Assert.True(provider ? providers.IsFormOpen : models.IsFormOpen);
        repository.AfterWriteAsync = null;
        await command.ExecuteAsync(null);

        Assert.Equal(3, provider ? repository.Store.Providers.Count : repository.Store.Models.Count);
        Assert.NotEqual(previousId, provider ? repository.Store.Providers[2].Id : repository.Store.Models[2].Id);
    }

    private static void OpenDraft(bool provider, ProviderSettingsViewModel providers, ModelSettingsViewModel models)
    {
        if (provider)
        {
            providers.OpenCreateFormCommand.Execute(null);
            providers.Name = "initial-provider";
            providers.ApiKey = "fixture-recovery-key";
        }
        else
        {
            models.OpenCreateFormCommand.Execute(null);
            models.ModelId = "fixture-recovery-model";
            models.DisplayName = "initial-model";
            models.ReasoningEffort = "off";
        }
    }
}
