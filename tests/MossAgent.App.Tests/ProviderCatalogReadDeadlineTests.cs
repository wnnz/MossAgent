using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProviderCatalogReadDeadlineTests
{
    [Fact]
    public void DisposedCatalog_RetainsItsCanceledLifetimeTokenAndRejectsReads()
    {
        var (repository, _, _) = ModelPickerReloadTests.Profiles();
        using var catalog = new ProviderModelCatalogViewModel(repository);
        var token = catalog.LifetimeToken;

        catalog.Dispose();

        Assert.Equal(token, catalog.LifetimeToken);
        Assert.True(token.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => catalog.CreateReadOperation());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousCompletionAfterCancellation_DoesNotPublishOrUnlockTheCatalog(bool providers)
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        using var catalog = new ProviderModelCatalogViewModel(repository, TimeSpan.FromMilliseconds(50));
        Assert.True(await catalog.RefreshAsync());
        var snapshot = Assert.Single(catalog.Groups);
        var publications = 0;
        catalog.SnapshotChanged += () => publications++;
        if (providers) repository.ProviderLoader = () => Task.FromResult(
            CancellationObservedSnapshot.Read(repository.ProviderReadToken, new[] { provider with { Name = "late" } }));
        else repository.ModelLoader = _ => Task.FromResult(
            CancellationObservedSnapshot.Read(repository.ModelReadToken, new[] { model with { DisplayName = "late" } }));

        var loaded = await catalog.RefreshAsync();

        Assert.False(loaded);
        Assert.True((providers ? repository.ProviderReadToken : repository.ModelReadToken).IsCancellationRequested);
        Assert.True(catalog.HasLoadError);
        Assert.False(catalog.IsLoading);
        Assert.False(catalog.IsAvailable);
        Assert.False(catalog.CanMutate);
        Assert.Same(snapshot, Assert.Single(catalog.Groups));
        Assert.Equal(provider, Assert.Single(catalog.Providers));
        Assert.Equal(model, Assert.Single(snapshot.Models));
        Assert.Equal(0, publications);
        repository.ProviderLoader = null;
        repository.ModelLoader = null;
        Assert.True(await catalog.RefreshAsync());
        Assert.True(catalog.CanMutate);
        Assert.False(catalog.HasLoadError);
    }
}
