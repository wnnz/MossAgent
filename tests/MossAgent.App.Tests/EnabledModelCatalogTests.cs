using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class EnabledModelCatalogTests
{
    [Fact]
    public async Task ReadAsync_FiltersDisabledAndForeignModelsWithoutReadingDisabledProviders()
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        var disabledProvider = provider with { Id = Guid.NewGuid(), IsEnabled = false };
        repository.Providers.Add(disabledProvider);
        var reads = new List<Guid>();
        repository.ModelLoader = id =>
        {
            reads.Add(id);
            return Task.FromResult<IReadOnlyList<ModelProfile>>([
                model,
                model with { Id = Guid.NewGuid(), IsEnabled = false },
                model with { Id = Guid.NewGuid(), ProviderId = disabledProvider.Id }
            ]);
        };

        var items = await new EnabledModelCatalog(repository).ReadAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(items);
        Assert.Same(provider, item.Provider);
        Assert.Same(model, item.Model);
        Assert.Equal([provider.Id], reads);
        Assert.Equal(TestContext.Current.CancellationToken, repository.ProviderReadToken);
        Assert.Equal(TestContext.Current.CancellationToken, repository.ModelReadToken);
    }

    [Fact]
    public async Task AgentOptions_AreSortedAndPreserveProviderProtocolAndRecordEquality()
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        var second = provider with { Id = Guid.NewGuid(), Name = "A", Protocol = ProviderProtocol.AnthropicMessages };
        repository.Providers.Add(second);
        repository.Models.Add(model with { Id = Guid.NewGuid(), ProviderId = second.Id, DisplayName = "other" });
        var catalog = new EnabledModelCatalog(repository);

        var options = await catalog.ReadAgentOptionsAsync(TestContext.Current.CancellationToken);
        var reloaded = await catalog.ReadAgentOptionsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(options, reloaded);
        Assert.NotSame(options[0], reloaded[0]);
        Assert.Equal(second.Id, options[0].Model.ProviderId);
        Assert.Equal(second.Protocol, options[0].Protocol);
        Assert.Equal("A · other (fixture-model)", options[0].Label);
        Assert.Equal(provider.Id, options[1].Model.ProviderId);
    }

    [Fact]
    public async Task ReadAsync_DoesNotShareMutableSelectionBetweenConsumers()
    {
        var (repository, _, _) = ModelPickerReloadTests.Profiles();
        var catalog = new EnabledModelCatalog(repository);
        var first = Assert.Single(await catalog.ReadAsync(TestContext.Current.CancellationToken));
        var second = Assert.Single(await catalog.ReadAsync(TestContext.Current.CancellationToken));

        first.IsSelected = true;

        Assert.NotSame(first, second);
        Assert.False(second.IsSelected);
    }

    [Fact]
    public async Task ReadAsync_PreCancelledReadDoesNotAccessRepository()
    {
        var (repository, _, _) = ModelPickerReloadTests.Profiles();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new EnabledModelCatalog(repository).ReadAsync(cancellation.Token));

        Assert.Equal(0, repository.ProviderReadCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadAsync_CancelsEvenWhenRepositoryIgnoresToken(bool delayProviders)
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        var providers = new TaskCompletionSource<IReadOnlyList<AiProvider>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var models = new TaskCompletionSource<IReadOnlyList<ModelProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (delayProviders) repository.ProviderLoader = () => providers.Task;
        else repository.ModelLoader = _ => models.Task;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var read = new EnabledModelCatalog(repository).ReadAsync(cancellation.Token);
        Assert.False(read.IsCompleted);

        cancellation.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read.WaitAsync(TestContext.Current.CancellationToken));
            Assert.Equal(cancellation.Token, delayProviders ? repository.ProviderReadToken : repository.ModelReadToken);
        }
        finally
        {
            providers.TrySetResult([provider]);
            models.TrySetResult([model]);
        }
    }

    [Fact]
    public async Task ReadAsync_FailedReloadDoesNotMutateAnEarlierSnapshot()
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        var second = provider with { Id = Guid.NewGuid(), Name = "second" };
        repository.Providers.Add(second);
        repository.Models.Add(model with { Id = Guid.NewGuid(), ProviderId = second.Id });
        var catalog = new EnabledModelCatalog(repository);
        var snapshot = await catalog.ReadAsync(TestContext.Current.CancellationToken);
        repository.ModelLoader = id => id == provider.Id
            ? Task.FromResult<IReadOnlyList<ModelProfile>>([model with { DisplayName = "partial" }])
            : Task.FromException<IReadOnlyList<ModelProfile>>(new InvalidOperationException("fixture-read-error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.ReadAsync(TestContext.Current.CancellationToken));

        Assert.Equal(2, snapshot.Count);
        Assert.Same(model, snapshot[0].Model);
    }

    [Fact]
    public async Task ReadAsync_PreservesProviderAndModelOrderIncludingDuplicates()
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        var secondProvider = provider with { Id = Guid.NewGuid(), Name = "A" };
        var secondModel = model with { Id = Guid.NewGuid(), ProviderId = secondProvider.Id };
        repository.Providers.Add(secondProvider);
        repository.ModelLoader = id => Task.FromResult<IReadOnlyList<ModelProfile>>(id == provider.Id
            ? [model, model with { IsEnabled = false }, model, secondModel]
            : [secondModel, model]);

        var items = await new EnabledModelCatalog(repository).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { model, model, secondModel }, items.Select(item => item.Model));
        Assert.Same(provider, items[0].Provider);
        Assert.Same(provider, items[1].Provider);
        Assert.Same(secondProvider, items[2].Provider);
        Assert.NotSame(items[0], items[1]);
    }

    [Fact]
    public async Task ReadAsync_CancellationAfterLastRepositoryReadDoesNotPublishASnapshot()
    {
        var (repository, _, model) = ModelPickerReloadTests.Profiles();
        var catalog = new EnabledModelCatalog(repository);
        var snapshot = await catalog.ReadAsync(TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        repository.ModelLoader = _ =>
        {
            cancellation.Cancel();
            return Task.FromResult<IReadOnlyList<ModelProfile>>([model with { DisplayName = "cancelled" }]);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalog.ReadAsync(cancellation.Token));

        Assert.Same(model, Assert.Single(snapshot).Model);
    }
}
