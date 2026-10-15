using Microsoft.Extensions.DependencyInjection;
using MossAgent.App;
using MossAgent.App.ViewModels;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelSettingsViewModelTests
{
    [Fact]
    public async Task CatalogRefresh_KeepsTheLatestProviderSelectionDuringRead()
    {
        var firstProvider = CreateProvider("first");
        var secondProvider = CreateProvider("second");
        var repository = new ModelReadProbeConfigurationRepository(
            [firstProvider, secondProvider],
            [CreateModel(firstProvider.Id, "first-model"), CreateModel(secondProvider.Id, "second-model")]);
        var providerSettings = new ProviderSettingsViewModel(repository);
        var viewModel = new ModelSettingsViewModel(
            repository, new FixedModelDiscoveryService([]), providerSettings);
        await viewModel.RefreshProvidersCommand.ExecuteAsync(null);

        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCount = 0;
        repository.BeforeModelReadAsync = providerId =>
        {
            if (providerId != firstProvider.Id || Interlocked.Exchange(ref readCount, 1) != 0)
                return Task.CompletedTask;
            readStarted.SetResult();
            return releaseRead.Task;
        };

        viewModel.SelectedProvider = secondProvider;
        viewModel.SelectedProvider = firstProvider;
        var refresh = viewModel.RefreshProvidersCommand.ExecuteAsync(null);
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        viewModel.SelectedProvider = secondProvider;
        Assert.Equal("second-model", Assert.Single(viewModel.Models).ModelId);

        releaseRead.SetResult();
        await refresh;
        Assert.Equal(secondProvider.Id, viewModel.SelectedProvider?.Id);
        Assert.Equal("second-model", Assert.Single(viewModel.Models).ModelId);
    }

    [Fact]
    public async Task ProviderGroupRefresh_PreservesExpansionChangedDuringRead()
    {
        var provider = CreateProvider("provider");
        var repository = new ModelReadProbeConfigurationRepository(
            [provider], [CreateModel(provider.Id, "model")]);
        var viewModel = new ModelSettingsViewModel(
            repository, new FixedModelDiscoveryService([]),
            new ProviderSettingsViewModel(repository));
        await viewModel.RefreshProvidersCommand.ExecuteAsync(null);
        var group = Assert.Single(viewModel.ProviderGroups);

        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCount = 0;
        repository.BeforeModelReadAsync = _ =>
        {
            if (Interlocked.Exchange(ref readCount, 1) != 0) return Task.CompletedTask;
            readStarted.SetResult();
            return releaseRead.Task;
        };

        var refresh = viewModel.RefreshProvidersCommand.ExecuteAsync(null);
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        viewModel.ToggleProviderCommand.Execute(group);
        Assert.True(group.IsExpanded);
        releaseRead.SetResult();
        await refresh;

        Assert.True(Assert.Single(viewModel.ProviderGroups).IsExpanded);
        Assert.Equal("model", Assert.Single(Assert.Single(viewModel.ProviderGroups).Models).ModelId);
    }

    [Fact]
    public async Task ProviderRefresh_KeepsOpenModelDraftForSameProvider()
    {
        var root = CreateRoot();
        try
        {
            var fixture = await CreateModelSettingsAsync(root);
            await using (fixture.Services)
            {
                fixture.ViewModel.AddModelForProviderCommand.Execute(
                    Assert.Single(fixture.ViewModel.ProviderGroups));
                fixture.ViewModel.ModelId = "draft-model";

                await fixture.ViewModel.RefreshProvidersCommand.ExecuteAsync(null);

                Assert.True(fixture.ViewModel.IsFormOpen);
                Assert.Equal(fixture.Provider.Id, fixture.ViewModel.SelectedProvider?.Id);
                Assert.Equal("draft-model", fixture.ViewModel.ModelId);

                fixture.ViewModel.CloseFormCommand.Execute(null);
                fixture.ViewModel.EditProviderModelCommand.Execute(fixture.Model);
                fixture.ViewModel.ContextLength = 256_000;
                await fixture.ViewModel.RefreshProvidersCommand.ExecuteAsync(null);

                Assert.True(fixture.ViewModel.IsFormOpen);
                Assert.Equal(fixture.Model.Id, fixture.ViewModel.SelectedModel?.Id);
                Assert.Equal(256_000, fixture.ViewModel.ContextLength);
            }
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NewlyAddedProvider_CanImmediatelySaveManualModel()
    {
        var root = CreateRoot();
        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>()
                .InitializeAsync(CancellationToken.None);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            var providerSettings = services.GetRequiredService<ProviderSettingsViewModel>();
            var modelSettings = services.GetRequiredService<ModelSettingsViewModel>();
            await modelSettings.RefreshProvidersCommand.ExecuteAsync(null);

            providerSettings.OpenCreateFormCommand.Execute(null);
            providerSettings.Name = "new-provider";
            providerSettings.BaseUrl = "https://new-provider.example/v1/";
            await providerSettings.SaveCommand.ExecuteAsync(null);

            var group = Assert.Single(modelSettings.ProviderGroups);
            modelSettings.AddModelForProviderCommand.Execute(group);
            modelSettings.ModelId = "new-model";
            await modelSettings.SaveModelCommand.ExecuteAsync(null);

            Assert.Contains(await repository.GetModelsAsync(group.Provider.Id, CancellationToken.None),
                model => model.ModelId == "new-model");
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ModelSettings_PersistGenerationParametersAndResetNewModelDefaults()
    {
        var root = CreateRoot();
        try
        {
            var fixture = await CreateModelSettingsAsync(root);
            await using (fixture.Services)
            {
                fixture.ViewModel.EditProviderModelCommand.Execute(fixture.Model);
                SetGenerationParameters(fixture.ViewModel);
                await fixture.ViewModel.SaveModelCommand.ExecuteAsync(null);
                var saved = Assert.Single(await fixture.Repository.GetModelsAsync(
                    fixture.Provider.Id, CancellationToken.None));
                AssertGenerationParameters(saved);
                fixture.ViewModel.AddModelForProviderCommand.Execute(
                    Assert.Single(fixture.ViewModel.ProviderGroups));
                AssertNewModelDefaults(fixture.ViewModel);
            }
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FetchModelsForProviderCommand_AddsMissingModelsAndPreservesExistingSettings()
    {
        var root = CreateRoot();
        try
        {
            var fixture = await CreateModelSettingsAsync(root);
            await using (fixture.Services)
            {
                var existing = fixture.Model with
                {
                    DisplayName = "Pinned model name",
                    ContextLength = 256_000,
                    MaximumOutputTokens = 24_000,
                    ReasoningEffort = "high",
                    Temperature = 0.7,
                    TopP = 0.9,
                    SupportsImages = true,
                    SupportsFiles = true,
                    IsDefault = true,
                    AdvancedParametersJson = "{\"reasoning\":{\"effort\":\"high\"}}"
                };
                await fixture.Repository.SaveModelAsync(existing, CancellationToken.None);
                var discovery = new FixedModelDiscoveryService(
                [new("parameter-model", "Updated name"), new("discovered-model", "Discovered")]);
                var viewModel = new ModelSettingsViewModel(
                    fixture.Repository, discovery,
                    fixture.Services.GetRequiredService<ProviderSettingsViewModel>());
                await viewModel.RefreshProvidersCommand.ExecuteAsync(null);
                var group = Assert.Single(viewModel.ProviderGroups);

                await viewModel.FetchModelsForProviderCommand.ExecuteAsync(group);

                Assert.Equal(fixture.Provider.Id, discovery.LastRequestedProvider?.Id);
                var models = await fixture.Repository.GetModelsAsync(
                    fixture.Provider.Id, CancellationToken.None);
                Assert.Equal(2, models.Count);
                Assert.Equal(existing, Assert.Single(models, model => model.ModelId == "parameter-model"));
                var added = Assert.Single(models, model => model.ModelId == "discovered-model");
                Assert.Equal("Discovered", added.DisplayName);
                Assert.Equal(128_000, added.ContextLength);
                Assert.Equal(8_192, added.MaximumOutputTokens);
                Assert.Equal("medium", added.ReasoningEffort);
                Assert.False(added.IsDefault);
                Assert.Contains("已同步", viewModel.Status);
                Assert.True(viewModel.HasStatus);
                Assert.False(viewModel.HasStatusError);
            }
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FetchModels_KeepsResultsWithRequestedProviderAfterSelectionChanges()
    {
        var root = CreateRoot();
        try
        {
            var fixture = await CreateModelSettingsAsync(root);
            await using (fixture.Services)
            {
                var otherProvider = CreateProvider("other");
                await fixture.Repository.SaveProviderAsync(otherProvider, CancellationToken.None);
                var discovery = new DeferredModelDiscoveryService();
                var viewModel = new ModelSettingsViewModel(fixture.Repository, discovery,
                    fixture.Services.GetRequiredService<ProviderSettingsViewModel>());
                await viewModel.RefreshProvidersCommand.ExecuteAsync(null);
                var group = Assert.Single(viewModel.ProviderGroups,
                    item => item.Provider.Id == fixture.Provider.Id);

                var fetch = viewModel.FetchModelsForProviderCommand.ExecuteAsync(group);
                await discovery.Started.WaitAsync(
                    TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                viewModel.SelectedProvider = otherProvider;
                viewModel.ContextLength = 999_999;
                viewModel.ReasoningEffort = "high";
                discovery.Complete([new("fetched-model", "Fetched model")]);
                await fetch;

                Assert.Equal(fixture.Provider.Id, discovery.RequestedProvider?.Id);
                var firstModels = await fixture.Repository.GetModelsAsync(
                    fixture.Provider.Id, CancellationToken.None);
                var fetched = Assert.Single(firstModels, model => model.ModelId == "fetched-model");
                Assert.Equal(128_000, fetched.ContextLength);
                Assert.Equal("medium", fetched.ReasoningEffort);
                Assert.Empty(await fixture.Repository.GetModelsAsync(otherProvider.Id, CancellationToken.None));
                Assert.Equal(otherProvider.Id, viewModel.SelectedProvider?.Id);
            }
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FetchModels_TimesOutWithoutChangingSavedModels()
    {
        var root = CreateRoot();
        try
        {
            var fixture = await CreateModelSettingsAsync(root);
            await using (fixture.Services)
            {
                var discovery = new DeferredModelDiscoveryService();
                var viewModel = new ModelSettingsViewModel(fixture.Repository, discovery,
                    fixture.Services.GetRequiredService<ProviderSettingsViewModel>(),
                    TimeSpan.FromMilliseconds(50));
                await viewModel.RefreshProvidersCommand.ExecuteAsync(null);

                await viewModel.FetchModelsForProviderCommand.ExecuteAsync(
                    Assert.Single(viewModel.ProviderGroups));

                Assert.Contains("超时", viewModel.Status);
                Assert.Equal(fixture.Model, Assert.Single(await fixture.Repository.GetModelsAsync(
                    fixture.Provider.Id, CancellationToken.None)));
            }
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<(
        ServiceProvider Services,
        IConfigurationRepository Repository,
        AiProvider Provider,
        ModelProfile Model,
        ModelSettingsViewModel ViewModel)> CreateModelSettingsAsync(string root)
    {
        var services = AppComposition.CreateServices(root);
        await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(CancellationToken.None);
        var repository = services.GetRequiredService<IConfigurationRepository>();
        var provider = CreateProvider("parameters");
        var model = CreateModel(provider.Id, "parameter-model");
        await repository.SaveProviderAsync(provider, CancellationToken.None);
        await repository.SaveModelAsync(model, CancellationToken.None);
        var viewModel = services.GetRequiredService<ModelSettingsViewModel>();
        await viewModel.RefreshProvidersCommand.ExecuteAsync(null);
        return (services, repository, provider, model, viewModel);
    }

    private static void SetGenerationParameters(ModelSettingsViewModel viewModel)
    {
        viewModel.ContextLength = 256_000;
        viewModel.MaximumOutputTokens = 24_000;
        viewModel.ReasoningEffort = "high";
        viewModel.Temperature = 0.7;
        viewModel.TopP = 0.9;
        viewModel.SupportsImages = true;
        viewModel.SupportsFiles = true;
        viewModel.AdvancedJson = "{\"reasoning\":{\"summary\":\"auto\"}}";
    }

    private static void AssertGenerationParameters(ModelProfile model)
    {
        Assert.Equal(256_000, model.ContextLength);
        Assert.Equal(24_000, model.MaximumOutputTokens);
        Assert.Equal("high", model.ReasoningEffort);
        Assert.Equal(0.7, model.Temperature);
        Assert.Equal(0.9, model.TopP);
        Assert.True(model.SupportsImages);
        Assert.True(model.SupportsFiles);
        Assert.Equal("{\"reasoning\":{\"summary\":\"auto\"}}", model.AdvancedParametersJson);
    }

    private static void AssertNewModelDefaults(ModelSettingsViewModel viewModel)
    {
        Assert.Equal(128_000, viewModel.ContextLength);
        Assert.Equal(8_192, viewModel.MaximumOutputTokens);
        Assert.Equal("medium", viewModel.ReasoningEffort);
        Assert.Null(viewModel.Temperature);
        Assert.Null(viewModel.TopP);
    }

    private static AiProvider CreateProvider(string name) =>
        new(Guid.NewGuid(), name, ProviderProtocol.OpenAiResponses,
            new Uri($"https://{name}.example/v1/"), "test-key", null, false, true,
            new Dictionary<string, string>());

    private static ModelProfile CreateModel(Guid providerId, string id) =>
        new(Guid.NewGuid(), providerId, id, id, 128_000, 8_192, "medium",
            null, null, false, false, false, true, "{}");

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
