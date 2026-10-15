using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProviderModelCatalogDraftTests
{
    [Fact]
    public async Task ProviderRefreshPreservesDraftAndUsesTheLatestSavedKeyWhenLeftBlank()
    {
        var repository = Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        providers.EditProviderCommand.Execute(repository.Providers[0]);
        providers.Name = "draft-name";
        providers.BaseUrl = "https://draft.example.test/v1/";
        repository.Providers[0] = repository.Providers[0] with { ApiKey = "latest-fixture-key", Name = "saved-name" };

        await providers.RefreshCommand.ExecuteAsync(null);

        Assert.True(providers.IsFormOpen);
        Assert.Equal("draft-name", providers.Name);
        Assert.Equal("https://draft.example.test/v1/", providers.BaseUrl);
        Assert.Empty(providers.ApiKey);
        await providers.SaveCommand.ExecuteAsync(null);
        var saved = Assert.Single(repository.Providers);
        Assert.Equal("draft-name", saved.Name);
        Assert.Equal("latest-fixture-key", saved.ApiKey);
        Assert.DoesNotContain("fixture-key", providers.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletedProviderCannotRebindAnOpenModelDraftOrBeResurrected(bool createModel)
    {
        var repository = Repository();
        var original = repository.Providers[0];
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        providers.EditProviderCommand.Execute(original);
        providers.Name = "provider-draft";
        if (createModel) models.AddModelForProviderCommand.Execute(models.ProviderGroups[0]);
        else models.EditProviderModelCommand.Execute(repository.Models[0]);
        models.ModelId = "model-draft";
        repository.Providers.Clear();
        var remaining = ProviderSettingsRowTestData.Provider();
        repository.Providers.Add(remaining);

        await models.RefreshProvidersCommand.ExecuteAsync(null);

        Assert.Equal(original.Id, models.SelectedProvider?.Id);
        Assert.Equal(original.Id, providers.SelectedItem?.Id);
        Assert.Equal("provider-draft", providers.Name);
        Assert.Equal("model-draft", models.ModelId);
        Assert.True(models.IsFormOpen);
        Assert.True(providers.IsFormOpen);
        Assert.True(models.IsEditorTargetUnavailable);
        Assert.True(providers.IsEditorTargetUnavailable);
        Assert.False(models.SaveModelCommand.CanExecute(null));
        Assert.False(providers.SaveCommand.CanExecute(null));
        await models.SaveModelCommand.ExecuteAsync(null);
        await providers.SaveCommand.ExecuteAsync(null);
        Assert.Equal(remaining, Assert.Single(repository.Providers));
        Assert.DoesNotContain(repository.Models, model => model.ModelId == "model-draft");
    }

    [Fact]
    public async Task DeletedModelRetainsDraftButCannotSaveOrDeleteTheMissingTarget()
    {
        var repository = Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        models.EditProviderModelCommand.Execute(repository.Models[0]);
        models.ContextLength = 256_000;
        var original = models.SelectedModel;
        repository.Models.Clear();

        await models.RefreshProvidersCommand.ExecuteAsync(null);

        Assert.Same(original, models.SelectedModel);
        Assert.Equal(256_000, models.ContextLength);
        Assert.True(models.IsEditorTargetUnavailable);
        Assert.False(models.DeleteModelCommand.CanExecute(null));
        await models.SaveModelCommand.ExecuteAsync(null);
        Assert.Empty(repository.Models);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForeignModelCannotBeSavedUnderTheSelectedProviderEvenOutsideTheForm(bool formOpen)
    {
        var repository = Repository();
        var other = ProviderSettingsRowTestData.Provider();
        repository.Providers.Add(other);
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        models.SelectedProvider = other;
        models.SelectedModel = repository.Models[0];
        models.IsFormOpen = formOpen;

        Assert.True(models.IsEditorTargetUnavailable);
        Assert.False(models.SaveModelCommand.CanExecute(null));
        await models.SaveModelCommand.ExecuteAsync(null);
        Assert.Equal(repository.Providers[0].Id, Assert.Single(repository.Models).ProviderId);
    }

    [Fact]
    public async Task ErrorKeepsBothDraftsAndDisablesActionsUntilSuccessfulRetry()
    {
        var repository = Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        providers.EditProviderCommand.Execute(repository.Providers[0]);
        models.EditProviderModelCommand.Execute(repository.Models[0]);
        providers.Name = "provider-draft";
        models.DisplayName = "model-draft";
        repository.ModelLoader = _ => Task.FromException<IReadOnlyList<ModelProfile>>(new Exception("fixture-secret"));

        await models.RefreshProvidersCommand.ExecuteAsync(null);

        Assert.True(providers.HasEditorNotice);
        Assert.True(models.HasEditorNotice);
        Assert.DoesNotContain("fixture-secret", models.EditorNotice);
        Assert.False(models.SaveModelCommand.CanExecute(null));
        Assert.False(providers.SaveCommand.CanExecute(null));
        Assert.False(models.DeleteProviderModelCommand.CanExecute(repository.Models[0]));
        Assert.False(providers.EditProviderCommand.CanExecute(repository.Providers[0]));
        repository.ModelLoader = null;
        await providers.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("provider-draft", providers.Name);
        Assert.Equal("model-draft", models.DisplayName);
        Assert.False(models.HasEditorNotice);
        Assert.True(models.SaveModelCommand.CanExecute(null));
        Assert.True(providers.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task MissingRowCannotOpenAnEditorWithStaleData()
    {
        var repository = Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        providers.EditProviderCommand.Execute(ProviderSettingsRowTestData.Provider());
        Assert.False(providers.IsFormOpen);
        Assert.Null(providers.SelectedItem);
    }

    [Fact]
    public async Task DisposedModelSettingsStopsSynchronizingAndNotifyingConsumers()
    {
        var repository = Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        var provider = models.SelectedProvider;
        var notificationCount = 0;
        models.ModelCatalogChanged += () => { notificationCount++; return Task.CompletedTask; };
        models.Dispose();
        repository.Providers[0] = repository.Providers[0] with { Name = "updated" };
        await providers.RefreshCommand.ExecuteAsync(null);

        Assert.Same(provider, models.SelectedProvider);
        Assert.Equal(0, notificationCount);
        Assert.False(models.RefreshProvidersCommand.CanExecute(null));
        Assert.False(models.SaveModelCommand.CanExecute(null));
    }

    [Fact]
    public async Task DiscoveryCannotSaveResultsAfterSettingsDisposal()
    {
        var repository = Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        var discovery = new DeferredModelDiscoveryService();
        var models = new ModelSettingsViewModel(repository, discovery, providers);
        var fetch = models.FetchModelsForProviderCommand.ExecuteAsync(models.ProviderGroups[0]);
        await discovery.Started.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        models.Dispose();
        await fetch.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        discovery.Complete([new("late-model", "Late model")]);

        Assert.Single(repository.Models);
        Assert.DoesNotContain(repository.Models, model => model.ModelId == "late-model");
    }

    [Fact]
    public async Task DeletedDiscoveryTargetCannotPersistResultsOrLeaveAFalseLoadingStatus()
    {
        var repository = Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        var discovery = new DeferredModelDiscoveryService();
        using var models = new ModelSettingsViewModel(repository, discovery, providers);
        var fetch = models.FetchModelsForProviderCommand.ExecuteAsync(models.ProviderGroups[0]);
        await discovery.Started.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        repository.Providers.Clear();
        await providers.RefreshCommand.ExecuteAsync(null);
        discovery.Complete([new("late-model", "Late model")]);
        await fetch;

        Assert.DoesNotContain(repository.Models, model => model.ModelId == "late-model");
        Assert.True(models.HasStatusError);
        Assert.Contains("重新获取模型", models.Status);
        Assert.DoesNotContain("正在从", models.Status);
    }

    private static ModelPickerProbeRepository Repository()
    {
        var repository = new ModelPickerProbeRepository();
        var provider = ProviderSettingsRowTestData.Provider() with { ApiKey = "saved-fixture-key" };
        repository.Providers.Add(provider);
        repository.Models.Add(ProviderSettingsRowTestData.Model(provider.Id));
        return repository;
    }
}
