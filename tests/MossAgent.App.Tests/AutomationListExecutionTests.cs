using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationListExecutionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task ForeignRunCommand_DoesNotCreateConversationOrRequestModel() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new AutomationListProbeRepository(fixture.Automations);
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog);
        await fixture.Automations.SaveAsync(AutomationListOwnershipTests.Schedule(fixture), fixture.Token);
        await fixture.Scheduler.StartAsync(fixture.Token);
        await page.ReloadAsync();
        var current = Assert.Single(page.Items);
        Assert.True(page.RunNowCommand.CanExecute(current));
        var foreign = new AutomationItemViewModel(current.Schedule, current.ProjectLabel, current.ModelLabel, false);
        Assert.False(page.RunNowCommand.CanExecute(foreign));
        await page.RunNowCommand.ExecuteAsync(foreign);
        Assert.Empty(fixture.Server.Requests);
        Assert.False(fixture.Scheduler.IsRunning(current.Schedule.Id));
        Assert.Empty(await fixture.Tasks.GetTasksAsync(current.Schedule.ProjectId, fixture.Token));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task StopRun_RemainsReachableWhileAReadIsPending() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        var repository = new AutomationListProbeRepository(fixture.Automations);
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog);
        await fixture.Automations.SaveAsync(AutomationListOwnershipTests.Schedule(fixture), fixture.Token);
        await fixture.Scheduler.StartAsync(fixture.Token);
        await page.ReloadAsync();
        await page.RunNowCommand.ExecuteAsync(Assert.Single(page.Items));
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var current = Assert.Single(page.Items);
        var schedules = await fixture.Automations.GetAllAsync(fixture.Token);
        var completion = new TaskCompletionSource<IReadOnlyList<AutomationSchedule>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.Read = _ => completion.Task;
        var pending = page.ReloadAsync();
        try
        {
            Assert.True(page.List.IsLoading);
            Assert.True(page.StopCommand.CanExecute(current));
            page.StopCommand.Execute(current);
            await fixture.WaitUntilAsync(() => !fixture.Scheduler.IsRunning(current.Schedule.Id));
            Assert.Equal(AutomationRunStatus.Cancelled, Assert.Single(await fixture.Automations.GetRunsAsync(current.Schedule.Id, 50, fixture.Token)).Status);
        }
        finally { completion.TrySetResult(schedules); await pending; }
        Assert.Single(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);
}
