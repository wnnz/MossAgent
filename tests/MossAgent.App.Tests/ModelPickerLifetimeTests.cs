using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelPickerLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoncooperativeReadTimeout_IsBoundedAndForwardsCancellation(bool providers)
    {
        var (repository, _, model) = ModelPickerReloadTests.Profiles();
        using var picker = new ModelPickerViewModel(repository, TimeSpan.FromMilliseconds(50));
        await picker.ReloadAsync();
        var selected = picker.SelectedItem;
        var providerGate = new TaskCompletionSource<IReadOnlyList<AiProvider>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var modelGate = new TaskCompletionSource<IReadOnlyList<ModelProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (providers) repository.ProviderLoader = () => providerGate.Task;
        else repository.ModelLoader = _ => modelGate.Task;

        await picker.ReloadWithCancellationAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True((providers ? repository.ProviderReadToken : repository.ModelReadToken).IsCancellationRequested);
        Assert.True(picker.HasLoadError);
        Assert.False(picker.IsLoading);
        Assert.False(picker.CanUseSelectedModel);
        Assert.Same(selected, picker.SelectedItem);
        providerGate.TrySetResult(repository.Providers.ToArray());
        modelGate.TrySetResult([model with { DisplayName = "late" }]);
        repository.ProviderLoader = null;
        repository.ModelLoader = null;
        await picker.RefreshCommand.ExecuteAsync(null);
        Assert.True(picker.CanUseSelectedModel);
        Assert.Equal(model.DisplayName, picker.SelectedModelName);
    }

    [Fact]
    public async Task OwnerDisposal_CancelsTheReadWithoutWaitingForANoncooperativeRepository()
    {
        var (repository, _, model) = ModelPickerReloadTests.Profiles();
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var gate = new TaskCompletionSource<IReadOnlyList<ModelProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ModelLoader = _ => gate.Task;
        var pending = picker.ReloadWithCancellationAsync(TestContext.Current.CancellationToken);
        var token = repository.ModelReadToken;

        picker.Dispose();
        await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(token.IsCancellationRequested);
        var reads = repository.ProviderReadCalls;
        gate.SetResult([model with { DisplayName = "late" }]);
        await picker.ReloadAsync();
        Assert.Equal(reads, repository.ProviderReadCalls);
        Assert.False(picker.CanUseSelectedModel);
        Assert.False(picker.HasLoadError);
    }

    [Fact]
    public async Task DirectRefreshReentry_DoesNotCancelTheAcceptedOperation()
    {
        var (repository, _, model) = ModelPickerReloadTests.Profiles();
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var gate = new TaskCompletionSource<IReadOnlyList<ModelProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ModelLoader = _ => gate.Task;
        var pending = picker.RefreshCommand.ExecuteAsync(null);
        var token = repository.ModelReadToken;

        await picker.RefreshCommand.ExecuteAsync(null);

        Assert.False(token.IsCancellationRequested);
        gate.SetResult([model]);
        await pending;
        Assert.True(picker.CanUseSelectedModel);
    }

    [Fact]
    public async Task RefreshCancellation_ReachesTheRepositoryAndPublishesASafeRetryState()
    {
        var (repository, _, _) = ModelPickerReloadTests.Profiles();
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var gate = new TaskCompletionSource<IReadOnlyList<ModelProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ModelLoader = _ => gate.Task;
        var pending = picker.RefreshCommand.ExecuteAsync(null);
        var token = repository.ModelReadToken;

        picker.RefreshCommand.Cancel();
        await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(token.IsCancellationRequested);
        Assert.True(picker.HasLoadError);
        Assert.False(picker.CanUseSelectedModel);
        Assert.True(picker.RefreshCommand.CanExecute(null));
        gate.SetResult([]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationOrDisposal_DoesNotCancelAnotherPickersRead(bool dispose)
    {
        var (repository, _, model) = ModelPickerReloadTests.Profiles();
        using var first = new ModelPickerViewModel(repository);
        using var second = new ModelPickerViewModel(repository);
        await first.ReloadAsync();
        await second.ReloadAsync();
        using var owner = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var gate = new TaskCompletionSource<IReadOnlyList<ModelProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.ModelLoader = _ => gate.Task;
        var firstRead = first.ReloadWithCancellationAsync(owner.Token);
        var firstToken = repository.ModelReadToken;
        var secondRead = second.ReloadWithCancellationAsync(TestContext.Current.CancellationToken);
        var secondToken = repository.ModelReadToken;

        if (dispose) first.Dispose();
        else owner.Cancel();
        await firstRead.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(firstToken.IsCancellationRequested);
        Assert.False(secondToken.IsCancellationRequested);
        Assert.True(second.IsLoading);
        Assert.False(first.CanUseSelectedModel);
        gate.SetResult([model with { DisplayName = "independent" }]);
        await secondRead.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(second.CanUseSelectedModel);
        Assert.Equal("independent", second.SelectedModelName);
        Assert.Equal(model.DisplayName, first.SelectedModelName);
    }
}
