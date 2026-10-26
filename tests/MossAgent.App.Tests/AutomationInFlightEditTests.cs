using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationInFlightEditTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task SaveWhileRunInFlight_AnnouncesNextRunAdoptionAndKeepsClaimedGeneration() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        var repository = new AutomationListProbeRepository(fixture.Automations);
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations,
            fixture.Tasks, fixture.Workspace.Catalog);
        var stored = await fixture.AuthorizedAsync(AutomationListOwnershipTests.Schedule(fixture));
        await fixture.Automations.SaveAsync(stored, fixture.Token);
        await fixture.Scheduler.StartAsync(fixture.Token);
        await page.ReloadAsync();
        await page.RunNowCommand.ExecuteAsync(Assert.Single(page.Items));
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && page.Items.Any(item => item.IsRunning));
        Assert.True(fixture.Scheduler.IsRunning(stored.Id));

        await page.EditCommand.ExecuteAsync(Assert.Single(page.Items));
        Assert.True(page.Editor.IsOpen);
        page.Editor.Name = "运行中修改";
        await page.SaveCommand.ExecuteAsync(null);

        Assert.False(page.HasError, page.Error);
        Assert.False(page.Editor.IsOpen);
        Assert.Equal(AutomationText.Get("SavedWhileRunning"), page.Notice);
        var updated = Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token));
        Assert.Equal(2, updated.AuthorizationSnapshot!.Generation);
        Assert.True(fixture.Scheduler.IsRunning(stored.Id));

        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !fixture.Scheduler.IsRunning(stored.Id));
        var run = Assert.Single(await fixture.Automations.GetRunsAsync(stored.Id, 50, fixture.Token));
        Assert.Equal(AutomationRunStatus.Completed, run.Status);
        Assert.Equal(1, run.SnapshotGeneration);
        Assert.Single(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SaveWithoutInFlightRun_LeavesNoRunningNotice() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new AutomationListProbeRepository(fixture.Automations);
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations,
            fixture.Tasks, fixture.Workspace.Catalog);
        await AutomationSaveOwnershipTests.OpenNewAsync(page, fixture);

        await page.SaveCommand.ExecuteAsync(null);

        Assert.False(page.HasError, page.Error);
        Assert.False(page.Editor.IsOpen);
        Assert.False(page.HasNotice);
        var stored = Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token));
        Assert.Equal(1, stored.AuthorizationSnapshot!.Generation);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SavingADifferentIdlePlan_DoesNotClaimTheRunningPlanNotice() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        var repository = new AutomationListProbeRepository(fixture.Automations);
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations,
            fixture.Tasks, fixture.Workspace.Catalog);
        var running = await fixture.AuthorizedAsync(AutomationListOwnershipTests.Schedule(fixture));
        await fixture.Automations.SaveAsync(running, fixture.Token);
        var idle = await fixture.AuthorizedAsync(AutomationListOwnershipTests.Schedule(fixture) with { Name = "空闲计划" });
        await fixture.Automations.SaveAsync(idle, fixture.Token);
        await fixture.Scheduler.StartAsync(fixture.Token);
        await page.ReloadAsync();
        await page.RunNowCommand.ExecuteAsync(Assert.Single(page.Items, item => item.Schedule.Id == running.Id));
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        Assert.True(fixture.Scheduler.IsRunning(running.Id));
        Assert.False(fixture.Scheduler.IsRunning(idle.Id));

        await page.EditCommand.ExecuteAsync(Assert.Single(page.Items, item => item.Schedule.Id == idle.Id));
        page.Editor.Name = "空闲计划已修改";
        await page.SaveCommand.ExecuteAsync(null);

        Assert.False(page.HasError, page.Error);
        Assert.False(page.HasNotice);
        Assert.True(fixture.Scheduler.IsRunning(running.Id));
        fixture.Server.ReleaseResponses();
    }, TestContext.Current.CancellationToken);
}
