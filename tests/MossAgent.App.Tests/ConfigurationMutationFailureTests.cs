using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ConfigurationMutationFailureTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedSaveIsSafeAndKeepsTheOriginalDraftUntilRetry(bool provider)
    {
        var repository = Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        providers.EditProviderCommand.Execute(repository.Store.Providers[0]);
        models.EditProviderModelCommand.Execute(repository.Store.Models[0]);
        providers.Name = "provider-draft";
        models.DisplayName = "model-draft";
        repository.FailingOperation = provider ? "save-provider" : "save-model";

        var error = await Record.ExceptionAsync(() => provider
            ? providers.SaveCommand.ExecuteAsync(null) : models.SaveModelCommand.ExecuteAsync(null));

        Assert.Null(error);
        Assert.Equal("provider-draft", providers.Name);
        Assert.Equal("model-draft", models.DisplayName);
        Assert.True(provider ? providers.HasValidationError : models.HasStatusError);
        Assert.DoesNotContain("fixture-secret", provider ? providers.Status : models.Status);
        Assert.True(provider ? providers.IsFormOpen : models.IsFormOpen);
        repository.FailingOperation = null;
        await (provider ? providers.SaveCommand.ExecuteAsync(null) : models.SaveModelCommand.ExecuteAsync(null));
        Assert.False(provider ? providers.IsFormOpen : models.IsFormOpen);
        Assert.False(provider ? providers.HasValidationError : models.HasStatusError);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedDeleteKeepsSelectionAndDataAndDoesNotExposeRawError(bool provider)
    {
        var repository = Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        providers.EditProviderCommand.Execute(repository.Store.Providers[0]);
        models.EditProviderModelCommand.Execute(repository.Store.Models[0]);
        repository.FailingOperation = provider ? "delete-provider" : "delete-model";
        if (provider) await providers.DeleteCommand.ExecuteAsync(null);

        var error = await Record.ExceptionAsync(() => provider
            ? providers.DeleteCommand.ExecuteAsync(null) : models.DeleteModelCommand.ExecuteAsync(null));

        Assert.Null(error);
        Assert.Single(repository.Store.Providers);
        Assert.Single(repository.Store.Models);
        Assert.NotNull(provider ? (object?)providers.SelectedItem : models.SelectedModel);
        Assert.DoesNotContain("fixture-secret", provider ? providers.Status : models.Status);
        Assert.True(provider ? providers.HasValidationError : models.HasStatusError);
    }

    internal static ConfigurationMutationProbeRepository Repository()
    {
        var repository = new ConfigurationMutationProbeRepository();
        var provider = ProviderSettingsRowTestData.Provider();
        repository.Store.Providers.Add(provider);
        repository.Store.Models.Add(ProviderSettingsRowTestData.Model(provider.Id));
        return repository;
    }
}
