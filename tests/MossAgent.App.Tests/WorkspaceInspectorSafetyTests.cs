using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceInspectorSafetyTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("locked")]
    [InlineData("foreign")]
    [InlineData("null")]
    [InlineData("invalid-tab")]
    public Task RejectedNavigation_DoesNotChangeInspectorOwnership(string reason) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var task = await AddTaskAsync(fixture);
        fixture.Main.SelectInspectorTabCommand.Execute("browser");
        var catalog = fixture.Workspace.Catalog;
        if (reason == "locked") catalog.IsLocked = true;
        if (reason == "invalid-tab")
        {
            Assert.False(fixture.Main.SelectInspectorTabCommand.CanExecute("invalid"));
            fixture.Main.ActiveInspectorTab = "invalid";
        }
        else
        {
            var target = task with { Id = Guid.NewGuid(), ProjectId = reason == "foreign" ? Guid.NewGuid() : task.ProjectId };
            catalog.SelectTaskCommand.Execute(reason == "null" ? null : target);
        }
        Assert.Equal(task.Id, catalog.SelectedTask!.Id);
        Assert.True(fixture.Main.IsInspectorBrowser);
        Assert.True(fixture.Main.IsRightPanelOpen);
        catalog.IsLocked = false;
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RestoreNotifications_ExposeOnlyTheCompleteNewPresentation() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var first = await AddTaskAsync(fixture);
        fixture.Main.SelectInspectorTabCommand.Execute("browser");
        await AddTaskAsync(fixture);
        var observations = new List<(string Tab, bool Open, bool Context)>();
        fixture.Main.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainWindowViewModel.ActiveInspectorTab) or nameof(MainWindowViewModel.IsRightPanelOpen)
                or nameof(MainWindowViewModel.IsContextCardVisible) or nameof(MainWindowViewModel.InspectorTitle))
                observations.Add((fixture.Main.ActiveInspectorTab, fixture.Main.IsRightPanelOpen, fixture.Main.IsContextCardVisible));
        };
        fixture.Workspace.Catalog.SelectTaskCommand.Execute(first);
        Assert.True(observations.Count >= 4);
        Assert.All(observations, observation => Assert.Equal(("browser", true, false), observation));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CatalogReload_RestoresTheSelectedTaskWithoutLosingItsInspector() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var task = await AddTaskAsync(fixture);
        fixture.Main.SelectInspectorTabCommand.Execute("terminal");
        await fixture.Workspace.Catalog.ReloadAsync();
        await fixture.WaitUntilAsync(() => fixture.Workspace.Catalog.SelectedTask?.Id == task.Id);
        Assert.True(fixture.Main.IsInspectorTerminal);
        Assert.True(fixture.Main.IsRightPanelOpen);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ArchivingTask_DropsItsPresentationBeforeRestoringTheTask() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var task = await AddTaskAsync(fixture);
        fixture.Main.SelectInspectorTabCommand.Execute("browser");
        var management = fixture.Workspace.Catalog.TaskManagement;
        await management.ArchiveCommand.ExecuteAsync(task);
        Assert.Null(fixture.Workspace.Catalog.SelectedTask);
        management.SelectedArchivedTask = Assert.Single(management.ArchivedTasks);
        await management.RestoreCommand.ExecuteAsync(null);
        Assert.Equal(task.Id, fixture.Workspace.Catalog.SelectedTask!.Id);
        Assert.Equal("home", fixture.Main.ActiveInspectorTab);
        Assert.False(fixture.Main.IsRightPanelOpen);
        Assert.True(fixture.Main.IsContextCardVisible);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ScheduledRunCompletionOrCancellation_DoesNotStealCurrentInspector(bool cancel) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        var task = await AddTaskAsync(fixture);
        fixture.Main.SelectInspectorTabCommand.Execute("browser");
        fixture.Main.Pages.OpenAutomationsCommand.Execute(null);
        var model = fixture.SelectedModel;
        var schedule = new AutomationSchedule(Guid.NewGuid(), "isolated-background", "Synthetic read-only request.", Assert.IsType<Guid>(task.ProjectId),
            model.ProviderId, model.Id, AutomationFrequency.Daily, 1, 9, 0, DayOfWeek.Monday,
            TimeZoneInfo.Local.Id, ApprovalPolicy.ReadOnly, 30, false, DateTimeOffset.UtcNow.AddDays(1));
        Assert.True(await fixture.Automations.SaveAsync(await fixture.AuthorizedAsync(schedule), fixture.Token));
        await fixture.Scheduler.StartAsync(fixture.Token);
        Assert.True(await fixture.Scheduler.RunNowAsync(schedule.Id, fixture.Token));
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        Assert.True(fixture.Main.IsInspectorBrowser);
        Assert.True(fixture.Main.IsRightPanelOpen);
        Assert.Equal(task.Id, fixture.Workspace.Catalog.SelectedTask!.Id);
        if (cancel) await fixture.Scheduler.StopAsync();
        else
        {
            fixture.Server.ReleaseResponses();
            await fixture.WaitUntilAsync(() => !fixture.Scheduler.IsRunning(schedule.Id));
        }
        var run = Assert.Single(await fixture.Automations.GetRunsAsync(schedule.Id, 10, fixture.Token));
        Assert.Equal(cancel ? AutomationRunStatus.Cancelled : AutomationRunStatus.Completed, run.Status);
        Assert.NotEqual(task.Id, run.TaskId);
        Assert.Equal(task.Id, fixture.Workspace.Catalog.SelectedTask!.Id);
        Assert.True(fixture.Main.Pages.IsAutomationsOpen);
        fixture.Main.Pages.IsAutomationsOpen = false;
        Assert.True(fixture.Main.IsInspectorBrowser);
        Assert.True(fixture.Main.IsRightPanelOpen);
        Assert.False(fixture.Main.IsContextCardVisible);
    }, TestContext.Current.CancellationToken);

    private static async Task<AgentTask> AddTaskAsync(WorkspaceModelSelectionFixture fixture)
    {
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), fixture.Workspace.Catalog.SelectedProject!.Id, "inspector-safety",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
        fixture.Workspace.Catalog.UpsertTask(task);
        return task;
    }
}
