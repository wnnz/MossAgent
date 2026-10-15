using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ConfigurationMutationConcurrencyTests
{
    [Fact]
    public async Task DiscoveryCannotImportWhileAnotherEditorIsWriting()
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        var discovery = new DeferredModelDiscoveryService();
        using var models = new ModelSettingsViewModel(repository, discovery, providers);
        providers.EditProviderCommand.Execute(repository.Store.Providers[0]);
        var fetch = models.FetchModelsForProviderCommand.ExecuteAsync(models.ProviderGroups[0]);
        await discovery.Started.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWriteAsync = token => pending.Task.WaitAsync(token);
        var save = providers.SaveCommand.ExecuteAsync(null);
        try
        {
            discovery.Complete([new("not-imported", "Not imported")]);
            await fetch;
            Assert.Single(repository.Writes);
            Assert.DoesNotContain(repository.Store.Models, model => model.ModelId == "not-imported");
            Assert.True(models.HasStatusError);
            Assert.Contains("稍后重新获取", models.Status);
        }
        finally { pending.TrySetResult(); await save; }
    }

    [Fact]
    public async Task ProviderChangesDuringProxyValidationCannotMixOldAndNewFields()
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        providers.EditProviderCommand.Execute(repository.Store.Providers[0]);
        var pending = new TaskCompletionSource<IReadOnlyList<MossAgent.Domain.ProxyProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ProxyLoader = () => pending.Task;
        var save = providers.SaveCommand.ExecuteAsync(null);
        providers.Name = "latest-name";
        providers.BaseUrl = "https://latest.example.test/v1/";
        pending.SetResult([]);
        await save;

        Assert.Empty(repository.Writes);
        Assert.True(providers.HasValidationError);
        Assert.Contains("编辑内容已变化", providers.Status);
        Assert.True(providers.IsFormOpen);
        Assert.Equal("latest-name", providers.Name);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task PendingSaveCannotEraseNewOrEditedDrafts(bool provider, bool reopen)
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        providers.EditProviderCommand.Execute(repository.Store.Providers[0]);
        models.EditProviderModelCommand.Execute(repository.Store.Models[0]);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWriteAsync = token => pending.Task.WaitAsync(token);
        var originalProvider = repository.Store.Providers[0];
        var originalModel = repository.Store.Models[0];
        var save = provider ? providers.SaveCommand.ExecuteAsync(null) : models.SaveModelCommand.ExecuteAsync(null);

        if (reopen)
        {
            if (provider) providers.OpenCreateFormCommand.Execute(null);
            else models.OpenCreateFormCommand.Execute(null);
        }
        providers.Name = "latest-provider-draft";
        models.DisplayName = "latest-model-draft";
        pending.SetResult();
        await save;

        Assert.Equal("latest-provider-draft", providers.Name);
        Assert.Equal("latest-model-draft", models.DisplayName);
        Assert.True(provider ? providers.IsFormOpen : models.IsFormOpen);
        Assert.Equal(originalProvider.Name, Assert.Single(repository.Store.Providers).Name);
        Assert.Equal(originalModel.DisplayName, Assert.Single(repository.Store.Models).DisplayName);
    }

    [Fact]
    public async Task PendingSaveBlocksOtherMutationsAcrossBothEditors()
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        providers.EditProviderCommand.Execute(repository.Store.Providers[0]);
        models.EditProviderModelCommand.Execute(repository.Store.Models[0]);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWriteAsync = token => pending.Task.WaitAsync(token);
        var save = providers.SaveCommand.ExecuteAsync(null);
        try
        {
            Assert.True(providers.Catalog.Mutations.IsRunning);
            Assert.False(models.Catalog.CanMutate);
            Assert.False(models.SaveModelCommand.CanExecute(null));
            Assert.False(models.DeleteModelCommand.CanExecute(null));
            Assert.False(providers.DeleteCommand.CanExecute(null));
            await models.DeleteProviderModelCommand.ExecuteAsync(repository.Store.Models[0]);
            Assert.Single(repository.Writes);
        }
        finally { pending.TrySetResult(); await save; }
        Assert.True(models.Catalog.CanMutate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DisposalCancelsOnlyTheOwnedWriteAndDoesNotCommitLateResults(bool provider)
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        providers.EditProviderCommand.Execute(repository.Store.Providers[0]);
        models.EditProviderModelCommand.Execute(repository.Store.Models[0]);
        providers.Name = "not-committed-provider";
        models.DisplayName = "not-committed-model";
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BeforeWriteAsync = token => pending.Task.WaitAsync(token);
        var save = provider ? providers.SaveCommand.ExecuteAsync(null) : models.SaveModelCommand.ExecuteAsync(null);

        if (provider) providers.Dispose();
        else models.Dispose();
        await save.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        pending.TrySetResult();

        Assert.DoesNotContain(repository.Store.Providers, item => item.Name == "not-committed-provider");
        Assert.DoesNotContain(repository.Store.Models, item => item.DisplayName == "not-committed-model");
        Assert.False(providers.Catalog.Mutations.IsRunning);
        if (!provider) Assert.True(providers.Catalog.CanMutate);
    }

    [Fact]
    public async Task DeletingAnotherModelPreservesTheCurrentOpenDraft()
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        var second = ProviderSettingsRowTestData.Model(repository.Store.Providers[0].Id);
        repository.Store.Models.Add(second);
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        models.EditProviderModelCommand.Execute(repository.Store.Models[0]);
        models.DisplayName = "current-draft";
        var selectedId = models.SelectedModel!.Id;

        await models.DeleteProviderModelCommand.ExecuteAsync(second);

        Assert.Equal(selectedId, models.SelectedModel?.Id);
        Assert.Equal("current-draft", models.DisplayName);
        Assert.True(models.IsFormOpen);
        Assert.Equal(selectedId, Assert.Single(repository.Store.Models).Id);
    }
}
