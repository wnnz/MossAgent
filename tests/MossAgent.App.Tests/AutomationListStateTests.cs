using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationListStateTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task InitialReadFailureAndRetry_HaveExclusiveSafeStates() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var completion = new TaskCompletionSource<IReadOnlyList<AutomationSchedule>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new AutomationListProbeRepository(fixture.Automations) { Read = _ => completion.Task };
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog);
        var pending = page.ReloadAsync();
        Assert.True(page.List.ShowLoading);
        Assert.False(page.IsEmpty);
        Assert.False(page.List.ShowFailure);
        completion.SetException(new InvalidOperationException("untrusted-secret-marker"));
        await pending;
        Assert.False(page.List.ShowLoading);
        Assert.True(page.List.ShowFailure);
        Assert.False(page.IsEmpty);
        Assert.DoesNotContain("untrusted-secret-marker", page.List.LoadError!);
        Assert.False(page.CreateCommand.CanExecute(null));
        Assert.True(page.RefreshCommand.CanExecute(null));
        repository.Read = _ => Task.FromResult<IReadOnlyList<AutomationSchedule>>([]);
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.True(page.IsEmpty);
        Assert.False(page.List.HasLoadError);
        Assert.False(page.List.ShowLoading);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ReentrantLoadingObserver_CannotStartAParallelRead() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var completion = new TaskCompletionSource<IReadOnlyList<AutomationSchedule>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new AutomationListProbeRepository(fixture.Automations) { Read = _ => completion.Task };
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog);
        Task? reentrant = null;
        page.List.PropertyChanged += (_, args) =>
        { if (args.PropertyName == nameof(page.List.IsLoading) && page.List.IsLoading) reentrant = page.ReloadAsync(); };
        var pending = page.ReloadAsync();
        completion.SetResult([]);
        await pending;
        Assert.Same(pending, reentrant);
        Assert.Equal(1, repository.ReadCount);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task FailedRefresh_RetainsRowsButDisablesMutationsUntilSuccessfulRetry() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new AutomationListProbeRepository(fixture.Automations);
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog);
        await fixture.Automations.SaveAsync(AutomationListOwnershipTests.Schedule(fixture), fixture.Token);
        await page.ReloadAsync();
        var original = Assert.Single(page.Items);
        repository.Read = _ => throw new IOException("untrusted-path-and-secret");
        await page.ReloadAsync();
        Assert.True(page.List.ShowRetainedError);
        Assert.False(page.List.ShowFailure);
        Assert.Same(original, page.SelectedItem);
        Assert.False(page.ToggleCommand.CanExecute(original));
        await page.ToggleCommand.ExecuteAsync(original);
        Assert.Equal(0, repository.WriteCount);
        repository.Read = null;
        await page.ReloadAsync();
        Assert.False(page.List.HasLoadError);
        Assert.True(page.ToggleCommand.CanExecute(Assert.Single(page.Items)));
        Assert.False(page.ToggleCommand.CanExecute(original));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Dispose_CancelsPendingReadAndRejectsNoncooperativeLateSnapshot() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var completion = new TaskCompletionSource<IReadOnlyList<AutomationSchedule>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        var repository = new AutomationListProbeRepository(fixture.Automations) { Read = token => { observed = token; return completion.Task; } };
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog);
        var pending = page.ReloadAsync();
        Assert.True(observed.CanBeCanceled);
        page.Dispose();
        Assert.True(observed.IsCancellationRequested);
        Assert.False(pending.IsCompleted);
        Assert.False(page.RefreshCommand.CanExecute(null));
        completion.SetResult([AutomationListOwnershipTests.Schedule(fixture)]);
        await pending;
        Assert.Empty(page.Items);
        Assert.Null(page.SelectedItem);
        Assert.False(page.List.HasLoadError);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SchedulerRefreshDuringRead_CatchesUpWithoutParallelQueries() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var schedule = AutomationListOwnershipTests.Schedule(fixture);
        var completion = new TaskCompletionSource<IReadOnlyList<AutomationSchedule>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new AutomationListProbeRepository(fixture.Automations);
        repository.Read = _ => repository.ReadCount == 1 ? completion.Task
            : Task.FromResult<IReadOnlyList<AutomationSchedule>>([schedule with { Name = "最新计划" }]);
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog);
        var pending = page.ReloadAsync();
        var catchUp = page.List.ReloadAsync(requireFresh: true);
        Assert.Equal(1, repository.ReadCount);
        completion.SetResult([schedule]);
        await Task.WhenAll(pending, catchUp);
        Assert.Equal(2, repository.ReadCount);
        Assert.Equal("最新计划", Assert.Single(page.Items).Name);
        Assert.Equal(2, page.List.SnapshotVersion);
        Assert.False(page.List.IsLoading);
    }, TestContext.Current.CancellationToken);
}
