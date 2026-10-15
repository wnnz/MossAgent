using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProviderModelCatalogTests
{
    [Fact]
    public async Task LoadingIsNotEmpty_AndSuccessfulEmptySnapshotIsAvailable()
    {
        var delayed = DelayedProviders();
        var repository = new ModelPickerProbeRepository { ProviderLoader = () => delayed.Task };
        using var catalog = new ProviderModelCatalogViewModel(repository);
        var refresh = catalog.RefreshAsync();

        Assert.True(catalog.IsLoading);
        Assert.True(catalog.HasNotice);
        Assert.False(catalog.IsEmpty);
        Assert.False(catalog.IsAvailable);
        delayed.SetResult([]);
        Assert.True(await refresh);
        Assert.True(catalog.IsEmpty);
        Assert.True(catalog.IsAvailable);
        Assert.False(catalog.HasNotice);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadFailureRetainsTheEntireSnapshot_AndRetryRecovers(bool modelRead)
    {
        var provider = ProviderSettingsRowTestData.Provider();
        var model = ProviderSettingsRowTestData.Model(provider.Id);
        var repository = new ModelPickerProbeRepository();
        repository.Providers.Add(provider);
        repository.Models.Add(model);
        using var catalog = new ProviderModelCatalogViewModel(repository);
        Assert.True(await catalog.RefreshAsync());
        var group = Assert.Single(catalog.Groups);
        repository.Providers[0] = provider with { Name = "not-yet-published" };
        if (modelRead) repository.ModelLoader = _ => Task.FromException<IReadOnlyList<ModelProfile>>(Failure());
        else repository.ProviderLoader = () => Task.FromException<IReadOnlyList<AiProvider>>(Failure());

        Assert.False(await catalog.RefreshAsync());

        Assert.Same(provider, Assert.Single(catalog.Providers));
        Assert.Same(group, Assert.Single(catalog.Groups));
        Assert.Same(model, Assert.Single(group.Models));
        Assert.True(catalog.HasLoadError);
        Assert.False(catalog.IsEmpty);
        Assert.False(catalog.IsAvailable);
        Assert.Contains("重试", catalog.Notice);
        Assert.DoesNotContain("fixture-secret", catalog.Notice);
        repository.ProviderLoader = null;
        repository.ModelLoader = null;
        Assert.True(await catalog.RefreshAsync());
        Assert.Equal("not-yet-published", Assert.Single(catalog.Providers).Name);
        Assert.False(catalog.HasLoadError);
        Assert.True(catalog.IsAvailable);
    }

    [Fact]
    public async Task InitialFailureDoesNotPretendToBeASuccessfulEmptyCatalog()
    {
        var repository = new ModelPickerProbeRepository
        {
            ProviderLoader = () => Task.FromException<IReadOnlyList<AiProvider>>(Failure())
        };
        using var catalog = new ProviderModelCatalogViewModel(repository);

        Assert.False(await catalog.RefreshAsync());
        Assert.Empty(catalog.Groups);
        Assert.True(catalog.HasLoadError);
        Assert.False(catalog.IsEmpty);
        Assert.False(catalog.IsAvailable);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleCompletionCannotReplaceANewerSuccess(bool fail)
    {
        var delayed = DelayedProviders();
        var repository = new ModelPickerProbeRepository { ProviderLoader = () => delayed.Task };
        using var catalog = new ProviderModelCatalogViewModel(repository);
        var earlier = catalog.RefreshAsync();
        var provider = ProviderSettingsRowTestData.Provider();
        repository.ProviderLoader = () => Task.FromResult<IReadOnlyList<AiProvider>>([provider]);
        Assert.True(await catalog.RefreshAsync());

        if (fail) delayed.SetException(Failure());
        else delayed.SetResult([]);
        Assert.False(await earlier);

        Assert.Same(provider, Assert.Single(catalog.Providers));
        Assert.True(catalog.IsAvailable);
        Assert.False(catalog.HasLoadError);
        Assert.False(catalog.IsLoading);
    }

    [Fact]
    public async Task StaleSuccessCannotEraseANewerFailureOrThePreviousSnapshot()
    {
        var repository = new ModelPickerProbeRepository();
        var provider = ProviderSettingsRowTestData.Provider();
        repository.Providers.Add(provider);
        using var catalog = new ProviderModelCatalogViewModel(repository);
        await catalog.RefreshAsync();
        var delayed = DelayedProviders();
        repository.ProviderLoader = () => delayed.Task;
        var earlier = catalog.RefreshAsync();
        repository.ProviderLoader = () => Task.FromException<IReadOnlyList<AiProvider>>(Failure());
        Assert.False(await catalog.RefreshAsync());
        delayed.SetResult([]);
        Assert.False(await earlier);

        Assert.Same(provider, Assert.Single(catalog.Providers));
        Assert.True(catalog.HasLoadError);
        Assert.False(catalog.IsAvailable);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBoundsUncooperativeReadsAndRejectsLatePublication(bool dispose)
    {
        var delayed = DelayedProviders();
        var repository = new ModelPickerProbeRepository { ProviderLoader = () => delayed.Task };
        using var catalog = new ProviderModelCatalogViewModel(repository,
            dispose ? TimeSpan.FromMinutes(1) : TimeSpan.FromMilliseconds(30));
        var publicationCount = 0;
        catalog.SnapshotChanged += () => publicationCount++;
        var refresh = catalog.RefreshAsync();
        if (dispose) catalog.Dispose();

        Assert.False(await refresh.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.False(catalog.IsLoading);
        Assert.False(catalog.IsAvailable);
        Assert.False(catalog.IsEmpty);
        Assert.Equal(!dispose, catalog.HasLoadError);
        delayed.SetResult([ProviderSettingsRowTestData.Provider()]);
        Assert.Empty(catalog.Providers);
        Assert.Equal(0, publicationCount);
    }

    [Fact]
    public async Task SnapshotPublishesOnlyAfterAllModelsAndFiltersForeignOwnership()
    {
        var first = ProviderSettingsRowTestData.Provider();
        var second = ProviderSettingsRowTestData.Provider();
        var delayed = new TaskCompletionSource<IReadOnlyList<ModelProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new ModelPickerProbeRepository();
        repository.Providers.AddRange([first, second]);
        repository.ModelLoader = providerId => providerId == second.Id ? delayed.Task
            : Task.FromResult<IReadOnlyList<ModelProfile>>([ProviderSettingsRowTestData.Model(second.Id)]);
        using var catalog = new ProviderModelCatalogViewModel(repository);
        var refresh = catalog.RefreshAsync();

        Assert.Empty(catalog.Providers);
        Assert.Empty(catalog.Groups);
        delayed.SetResult([ProviderSettingsRowTestData.Model(second.Id)]);
        Assert.True(await refresh);
        Assert.Equal(2, catalog.Providers.Count);
        Assert.Empty(catalog.Groups[0].Models);
        Assert.Equal(second.Id, Assert.Single(catalog.Groups[1].Models).ProviderId);
    }

    private static TaskCompletionSource<IReadOnlyList<AiProvider>> DelayedProviders() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static Exception Failure() => new InvalidOperationException("fixture-secret-api-key-password");
}
