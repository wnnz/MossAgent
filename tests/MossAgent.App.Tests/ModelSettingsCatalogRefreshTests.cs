using CommunityToolkit.Mvvm.Input;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelSettingsCatalogRefreshTests
{
    [Theory]
    [InlineData("refresh", 1)]
    [InlineData("save", 2)]
    [InlineData("delete", 0)]
    [InlineData("delete-row", 0)]
    [InlineData("discover", 2)]
    [InlineData("discover-row", 2)]
    public async Task Command_RefreshesOnceBeforeNotifyingConsumers(string operation, int expectedCount)
    {
        var repository = CreateRepository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository,
            new FixedModelDiscoveryService([new("existing", "Ignored name"), new("discovered", "Discovered")]), providers);
        var command = PrepareCommand(models, operation);
        var readsBefore = repository.ProviderReadCalls;
        var notificationCount = 0;
        models.ModelCatalogChanged += () =>
        {
            notificationCount++;
            Assert.True(models.Catalog.IsAvailable);
            Assert.False(models.Catalog.Mutations.IsRunning);
            Assert.Equal(expectedCount, models.Models.Count);
            Assert.Equal(repository.Models, models.Models);
            return Task.CompletedTask;
        };

        await command.ExecuteAsync(Parameter(models, operation));

        Assert.Equal(readsBefore + 1, repository.ProviderReadCalls);
        Assert.Equal(1, notificationCount);
        Assert.Equal(expectedCount, repository.Models.Count);
        Assert.False(models.HasStatusError);
        if (operation == "save")
        {
            var saved = Assert.Single(repository.Models, model => model.ModelId == "manual");
            Assert.Equal("manual", saved.DisplayName);
            Assert.Equal("high", saved.ReasoningEffort);
        }
    }

    [Theory]
    [InlineData("refresh", 1)]
    [InlineData("save", 2)]
    [InlineData("delete", 0)]
    public async Task ReadBackFailure_PreservesTheSnapshotWithoutReplayingTheMutation(string operation, int expectedCount)
    {
        var repository = CreateRepository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        var command = PrepareCommand(models, operation);
        var previousSnapshot = models.Models.ToArray();
        repository.ModelLoader = _ => Task.FromException<IReadOnlyList<ModelProfile>>(new Exception("fixture-secret"));
        var notificationCount = 0;
        models.ModelCatalogChanged += () =>
        {
            notificationCount++;
            Assert.True(models.Catalog.HasLoadError);
            Assert.False(models.Catalog.IsLoading);
            return Task.CompletedTask;
        };

        await command.ExecuteAsync(null);

        Assert.Equal(1, notificationCount);
        Assert.Equal(previousSnapshot, models.Models);
        Assert.Equal(expectedCount, repository.Models.Count);
        Assert.False(models.SaveModelCommand.CanExecute(null));
        Assert.DoesNotContain("fixture-secret", models.EditorNotice);
        Assert.DoesNotContain("fixture-secret", models.Status);
    }

    [Fact]
    public async Task Refresh_DisposalDuringReadDoesNotNotifyOrReplaceEditorState()
    {
        var repository = CreateRepository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        var original = models.SelectedProvider;
        var read = new TaskCompletionSource<IReadOnlyList<AiProvider>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ProviderLoader = () => read.Task;
        var notificationCount = 0;
        models.ModelCatalogChanged += () => { notificationCount++; return Task.CompletedTask; };

        var refresh = models.RefreshProvidersCommand.ExecuteAsync(null);
        Assert.True(models.Catalog.IsLoading);
        models.Dispose();
        read.SetResult([repository.Providers[0] with { Name = "Changed provider" }]);
        await refresh.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Same(original, models.SelectedProvider);
        Assert.Equal(0, notificationCount);
        Assert.False(models.RefreshProvidersCommand.CanExecute(null));
        Assert.False(models.SaveModelCommand.CanExecute(null));
    }

    [Fact]
    public async Task RowActions_ResolveTheCurrentModelRatherThanAStaleRow()
    {
        var repository = CreateRepository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        var stale = repository.Models[0];
        repository.Models[0] = stale with { DisplayName = "Current name" };
        await models.RefreshProvidersCommand.ExecuteAsync(null);

        models.EditProviderModelCommand.Execute(stale);

        Assert.Same(repository.Models[0], models.SelectedModel);
        Assert.Equal("Current name", models.DisplayName);
        await models.DeleteProviderModelCommand.ExecuteAsync(stale);
        Assert.Empty(repository.Models);
        Assert.Contains("Current name", models.Status);
        Assert.False(models.IsFormOpen);
    }

    private static ModelPickerProbeRepository CreateRepository()
    {
        var repository = new ModelPickerProbeRepository();
        var provider = ProviderSettingsRowTestData.Provider();
        repository.Providers.Add(provider);
        repository.Models.Add(ProviderSettingsRowTestData.Model(provider.Id) with
        {
            ModelId = "existing",
            DisplayName = "Existing"
        });
        return repository;
    }

    private static IAsyncRelayCommand PrepareCommand(ModelSettingsViewModel models, string operation)
    {
        if (operation == "save")
        {
            models.AddModelForProviderCommand.Execute(models.ProviderGroups[0]);
            models.ModelId = " manual ";
            models.DisplayName = " ";
            models.ReasoningEffort = " HIGH ";
        }
        else if (operation == "delete") models.EditProviderModelCommand.Execute(models.Models[0]);
        return operation switch
        {
            "refresh" => models.RefreshProvidersCommand,
            "save" => models.SaveModelCommand,
            "delete" => models.DeleteModelCommand,
            "delete-row" => models.DeleteProviderModelCommand,
            "discover" => models.FetchModelsCommand,
            "discover-row" => models.FetchModelsForProviderCommand,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }

    private static object? Parameter(ModelSettingsViewModel models, string operation) => operation switch
    {
        "delete-row" => models.Models[0],
        "discover-row" => models.ProviderGroups[0],
        _ => null
    };
}
