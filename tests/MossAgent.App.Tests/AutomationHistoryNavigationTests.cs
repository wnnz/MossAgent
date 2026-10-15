using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationHistoryNavigationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("selection")]
    [InlineData("refresh")]
    [InlineData("dispose")]
    [InlineData("blocked")]
    [InlineData("locked")]
    [InlineData("hide-reopen")]
    [InlineData("block-unblock")]
    [InlineData("lock-unlock")]
    public Task LateTaskRead_CannotNavigateAfterItsScopeOrPermissionChanges(string change) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var item = AutomationHistoryTests.Item(fixture);
        var run = AutomationHistoryTests.Run(item);
        var pending = new TaskCompletionSource<IReadOnlyList<AgentTask>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken readToken = default;
        var projects = new TestWorkspaceRepository { GetTasksHandler = (_, token) => { readToken = token; return pending.Task; } };
        var repository = new AutomationHistoryProbeRepository { Read = (_, _) => Task.FromResult<IReadOnlyList<AutomationRun>>([run]) };
        using var history = new AutomationHistoryViewModel(repository, projects, fixture.Workspace.Catalog);
        var opened = 0;
        history.TaskOpened += () => opened++;
        await history.SelectAsync(item);
        var opening = history.OpenCommand.ExecuteAsync(Assert.Single(history.Runs));
        Assert.True(history.IsOpening);
        Assert.False(history.OpenCommand.CanExecute(history.Runs[0]));
        switch (change)
        {
            case "selection": await history.SelectAsync(AutomationHistoryTests.Item(fixture)); break;
            case "refresh": await history.SelectAsync(item); break;
            case "dispose": history.Dispose(); break;
            case "blocked": history.IsBlocked = true; break;
            case "locked": fixture.Workspace.Catalog.IsLocked = true; break;
            case "hide-reopen": history.IsVisible = false; history.IsVisible = true; break;
            case "block-unblock": history.IsBlocked = true; history.IsBlocked = false; break;
            case "lock-unlock": fixture.Workspace.Catalog.IsLocked = true; fixture.Workspace.Catalog.IsLocked = false; break;
        }
        Assert.True(readToken.IsCancellationRequested);
        Assert.True(history.IsOpening);
        Assert.False(history.OpenCommand.CanExecute(history.Runs.FirstOrDefault()));
        pending.SetResult([TaskFor(run, item)]);
        await opening;
        Assert.False(history.IsOpening);
        Assert.Equal(0, opened);
        Assert.Null(fixture.Workspace.Catalog.SelectedTask);
        Assert.False(history.HasOpenError);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("missing")]
    [InlineData("foreign-project")]
    [InlineData("failure")]
    public Task MissingOrInvalidTask_ReportsSafeActionErrorAndPreservesTheRows(string state) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var item = AutomationHistoryTests.Item(fixture);
        var run = AutomationHistoryTests.Run(item);
        var projects = new TestWorkspaceRepository
        {
            GetTasksHandler = (_, _) => state == "failure" ? throw new IOException("api-key=must-never-display")
                : Task.FromResult<IReadOnlyList<AgentTask>>(state == "missing" ? [] : [TaskFor(run, item) with { ProjectId = Guid.NewGuid() }])
        };
        var repository = new AutomationHistoryProbeRepository { Read = (_, _) => Task.FromResult<IReadOnlyList<AutomationRun>>([run]) };
        using var history = new AutomationHistoryViewModel(repository, projects, fixture.Workspace.Catalog);
        await history.SelectAsync(item);
        await history.OpenCommand.ExecuteAsync(Assert.Single(history.Runs));
        Assert.True(history.HasOpenError);
        Assert.Equal(state == "failure" ? AutomationText.Get("HistoryOpenError") : AutomationText.Get("TaskUnavailable"), history.OpenError);
        Assert.True(history.HasRuns);
        Assert.Single(history.Runs);
        Assert.Null(fixture.Workspace.Catalog.SelectedTask);
        Assert.DoesNotContain("must-never-display", history.OpenError!);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CurrentTask_OpensOnceAndCommandsRespectBlockingAndDisposal() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var item = AutomationHistoryTests.Item(fixture);
        var run = AutomationHistoryTests.Run(item);
        var projects = new TestWorkspaceRepository { GetTasksHandler = (_, _) => Task.FromResult<IReadOnlyList<AgentTask>>([TaskFor(run, item)]) };
        var repository = new AutomationHistoryProbeRepository { Read = (_, _) => Task.FromResult<IReadOnlyList<AutomationRun>>([run]) };
        using var history = new AutomationHistoryViewModel(repository, projects, fixture.Workspace.Catalog);
        var opened = 0;
        history.TaskOpened += () => opened++;
        await history.SelectAsync(item);
        var row = Assert.Single(history.Runs);
        history.IsBlocked = true;
        Assert.False(history.OpenCommand.CanExecute(row));
        Assert.False(history.RefreshCommand.CanExecute(null));
        history.IsBlocked = false;
        fixture.Workspace.Catalog.IsLocked = true;
        Assert.False(history.OpenCommand.CanExecute(row));
        fixture.Workspace.Catalog.IsLocked = false;
        Assert.True(history.OpenCommand.CanExecute(row));
        await history.OpenCommand.ExecuteAsync(row);
        Assert.Equal(run.TaskId, fixture.Workspace.Catalog.SelectedTask!.Id);
        Assert.Equal(1, opened);
        history.Dispose();
        Assert.False(history.OpenCommand.CanExecute(row));
        Assert.False(history.RefreshCommand.CanExecute(null));
        await history.OpenCommand.ExecuteAsync(row);
        Assert.Equal(1, opened);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    private static AgentTask TaskFor(AutomationRun run, AutomationItemViewModel item) => new(run.TaskId, item.Schedule.ProjectId,
        "合成运行记录", ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, run.StartedAt, run.StartedAt, null);

    [Fact]
    public Task ActualPageNavigation_TracksHistoryVisibilityWithoutDiscardingRecords() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var pages = fixture.Main.Pages;
        var automation = pages.Automations!;
        var schedule = AutomationHistoryTests.Item(fixture).Schedule;
        await fixture.Automations.SaveAsync(schedule, fixture.Token);
        var stored = Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token));
        var run = await fixture.Automations.TryClaimAsync(stored, DateTimeOffset.UtcNow, stored.NextRunAt, true, fixture.Token);
        await fixture.Automations.FinishAsync(run! with { Status = AutomationRunStatus.Completed, FinishedAt = DateTimeOffset.UtcNow }, fixture.Token);
        Assert.False(automation.History.IsVisible);
        pages.OpenAutomationsCommand.Execute(null);
        await automation.ReloadAsync();
        await fixture.WaitUntilAsync(() => !automation.History.IsLoading && automation.Runs.Count == 1);
        var row = Assert.Single(automation.Runs);
        Assert.True(automation.History.IsVisible);
        Assert.True(automation.OpenRunCommand.CanExecute(row));
        pages.NewConversationCommand.Execute(null);
        Assert.False(automation.History.IsVisible);
        Assert.False(automation.OpenRunCommand.CanExecute(row));
        Assert.Single(automation.Runs);
        pages.OpenAutomationsCommand.Execute(null);
        await automation.ReloadAsync();
        await fixture.WaitUntilAsync(() => !automation.History.IsLoading && automation.Runs.Count == 1);
        Assert.True(automation.OpenRunCommand.CanExecute(Assert.Single(automation.Runs)));
        Assert.False(automation.OpenRunCommand.CanExecute(row));
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);
}
