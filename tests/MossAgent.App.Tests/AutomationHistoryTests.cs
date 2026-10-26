using System.Collections.Specialized;
using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationHistoryTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task LoadingAndFailure_NeverLookLikeEmptyHistoryAndRetryReadsTheCurrentPlan() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var pending = new TaskCompletionSource<IReadOnlyList<AutomationRun>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new AutomationHistoryProbeRepository { Read = (_, _) => pending.Task };
        using var history = new AutomationHistoryViewModel(repository, fixture.Tasks, fixture.Workspace.Catalog);
        Assert.False(history.HasSelection);
        Assert.False(history.IsEmpty);
        Assert.False(history.RefreshCommand.CanExecute(null));
        var item = Item(fixture);
        var loading = history.SelectAsync(item);
        Assert.True(history.IsLoading);
        Assert.False(history.IsEmpty);
        Assert.False(history.HasRuns);
        Assert.False(history.RefreshCommand.CanExecute(null));
        Assert.Equal(item.Name, history.SelectionName);
        pending.SetException(new IOException("api-key=must-never-display"));
        await loading;
        Assert.True(history.HasLoadError);
        Assert.False(history.IsLoading);
        Assert.False(history.IsEmpty);
        Assert.DoesNotContain("must-never-display", history.LoadError!);
        repository.Read = (_, _) => Task.FromResult<IReadOnlyList<AutomationRun>>([]);
        await history.RefreshCommand.ExecuteAsync(null);
        Assert.False(history.HasLoadError);
        Assert.True(history.IsEmpty);
        Assert.All(repository.Reads, read => { Assert.Equal(item.Schedule.Id, read.ScheduleId); Assert.Equal(50, read.Limit); });
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SelectionChange_CancelsOldReadAndRejectsItsLateSuccessOrError(bool fail) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var first = Item(fixture);
        var second = Item(fixture);
        var pending = new TaskCompletionSource<IReadOnlyList<AutomationRun>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = Run(second);
        var repository = new AutomationHistoryProbeRepository
        {
            Read = (id, _) => id == first.Schedule.Id ? pending.Task : Task.FromResult<IReadOnlyList<AutomationRun>>([expected])
        };
        using var history = new AutomationHistoryViewModel(repository, fixture.Tasks, fixture.Workspace.Catalog);
        var loading = history.SelectAsync(first);
        await history.SelectAsync(second);
        Assert.True(repository.Reads[0].Token.IsCancellationRequested);
        if (fail) pending.SetException(new IOException("old-error"));
        else pending.SetResult([Run(first)]);
        await loading;
        Assert.Equal(expected, Assert.Single(history.Runs).Run);
        Assert.False(history.HasLoadError);
        Assert.False(history.IsLoading);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Results_AreBoundedAndForeignScheduleRowsCannotNavigate() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var item = Item(fixture);
        var foreign = Run(Item(fixture));
        var repository = new AutomationHistoryProbeRepository
        {
            Read = (_, _) => Task.FromResult<IReadOnlyList<AutomationRun>>([foreign, .. Enumerable.Range(0, 60).Select(_ => Run(item))])
        };
        using var history = new AutomationHistoryViewModel(repository, fixture.Tasks, fixture.Workspace.Catalog);
        await history.SelectAsync(item);
        Assert.Equal(50, history.Runs.Count);
        Assert.All(history.Runs, run => Assert.Equal(item.Schedule.Id, run.Run.ScheduleId));
        var detached = new AutomationRunViewModel(history.Runs[0].Run);
        Assert.False(history.OpenCommand.CanExecute(detached));
        Assert.False(history.OpenCommand.CanExecute(new AutomationRunViewModel(foreign)));
        var old = history.Runs[0];
        await history.SelectAsync(null);
        Assert.False(history.OpenCommand.CanExecute(old));
        await history.OpenCommand.ExecuteAsync(old);
        Assert.Null(fixture.Workspace.Catalog.SelectedTask);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DisposeOrClearSelection_RejectsPendingHistory(bool dispose) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var item = Item(fixture);
        var pending = new TaskCompletionSource<IReadOnlyList<AutomationRun>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new AutomationHistoryProbeRepository { Read = (_, _) => pending.Task };
        using var history = new AutomationHistoryViewModel(repository, fixture.Tasks, fixture.Workspace.Catalog);
        var loading = history.SelectAsync(item);
        if (dispose) history.Dispose();
        else await history.SelectAsync(null);
        Assert.True(repository.Reads[0].Token.IsCancellationRequested);
        pending.SetResult([Run(item)]);
        await loading;
        Assert.False(history.HasSelection);
        Assert.False(history.IsLoading);
        Assert.False(history.IsEmpty);
        Assert.Empty(history.Runs);
        Assert.Equal(AutomationText.SelectSchedule, history.SelectionName);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task LoadingAndRefresh_ResetTheHistoryOnlyOnce(bool empty) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var item = Item(fixture);
        var run = Run(item);
        var repository = new AutomationHistoryProbeRepository
        {
            Read = (_, _) => Task.FromResult<IReadOnlyList<AutomationRun>>(empty ? [] : [run])
        };
        using var history = new AutomationHistoryViewModel(repository, fixture.Tasks, fixture.Workspace.Catalog);
        var resets = 0;
        history.Runs.CollectionChanged += (_, args) => { if (args.Action == NotifyCollectionChangedAction.Reset) resets++; };

        await history.SelectAsync(item);

        Assert.Equal(1, resets);
        var previous = history.Runs.FirstOrDefault();
        await history.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, resets);
        Assert.Equal(empty ? 0 : 1, history.Runs.Count);
        if (!empty)
        {
            Assert.NotSame(previous, history.Runs[0]);
            Assert.Same(run, history.Runs[0].Run);
            Assert.False(history.OpenCommand.CanExecute(previous));
        }
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MixedHistory_KeepsDuplicateOrderAndStopsReadingAtFiftyOwnedRuns() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var item = Item(fixture);
        var repeated = Run(item);
        var foreign = Run(Item(fixture));
        var remaining = Enumerable.Range(0, 60).Select(_ => Run(item)).ToArray();
        var counted = new CountingReadOnlyList<AutomationRun>([foreign, repeated, foreign, repeated, .. remaining]);
        var repository = new AutomationHistoryProbeRepository { Read = (_, _) => Task.FromResult<IReadOnlyList<AutomationRun>>(counted) };
        using var history = new AutomationHistoryViewModel(repository, fixture.Tasks, fixture.Workspace.Catalog);

        await history.SelectAsync(item);

        Assert.Equal(52, counted.ItemReads);
        Assert.Equal(new[] { repeated, repeated }.Concat(remaining.Take(48)), history.Runs.Select(row => row.Run));
        Assert.NotSame(history.Runs[0], history.Runs[1]);
        Assert.All(history.Runs, row => Assert.Equal(item.Schedule.Id, row.Run.ScheduleId));
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Rows_LabelTheClaimedGenerationAndFlagEarlierSnapshotGenerations() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var item = Item(fixture);
        var authorized = await fixture.AuthorizedAsync(item.Schedule, generation: 2);
        var selected = new AutomationItemViewModel(authorized, item.ProjectLabel, item.ModelLabel, false);
        var stale = Run(item) with { SnapshotGeneration = 1 };
        var current = Run(item) with { SnapshotGeneration = 2 };
        var legacy = Run(item);
        var repository = new AutomationHistoryProbeRepository
        {
            Read = (_, _) => Task.FromResult<IReadOnlyList<AutomationRun>>([stale, current, legacy])
        };
        using var history = new AutomationHistoryViewModel(repository, fixture.Tasks, fixture.Workspace.Catalog);

        await history.SelectAsync(selected);

        Assert.Equal(3, history.Runs.Count);
        Assert.Equal(AutomationText.Format("RunGeneration", 1), history.Runs[0].GenerationLabel);
        Assert.True(history.Runs[0].IsStaleGeneration);
        Assert.True(history.Runs[0].NeedsAttention);
        Assert.Equal(AutomationText.Get("RunStaleGenerationHint"), history.Runs[0].Detail);
        Assert.Equal(AutomationText.Format("RunGeneration", 2), history.Runs[1].GenerationLabel);
        Assert.False(history.Runs[1].IsStaleGeneration);
        Assert.False(history.Runs[1].NeedsAttention);
        Assert.False(history.Runs[1].HasDetail);
        Assert.Equal(AutomationText.Get("RunGenerationUnknown"), history.Runs[2].GenerationLabel);
        Assert.False(history.Runs[2].IsStaleGeneration);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    internal static AutomationItemViewModel Item(WorkspaceModelSelectionFixture fixture) => new(new AutomationSchedule(
        Guid.NewGuid(), "合成计划", "只读检查项目", fixture.Workspace.Catalog.SelectedProject!.Id,
        fixture.SelectedProvider.Id, fixture.SelectedModel.Id, AutomationFrequency.Daily, 1, 9, 0, DayOfWeek.Monday,
        TimeZoneInfo.Local.Id, ApprovalPolicy.ReadOnly, 30, false, DateTimeOffset.UtcNow.AddDays(1)), "合成项目", "合成模型", false);

    internal static AutomationRun Run(AutomationItemViewModel item) => new(Guid.NewGuid(), item.Schedule.Id, Guid.NewGuid(),
        DateTimeOffset.UtcNow, AutomationRunStatus.Completed, true, DateTimeOffset.UtcNow);
}
