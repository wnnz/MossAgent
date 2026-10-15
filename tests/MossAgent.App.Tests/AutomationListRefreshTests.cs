using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationListRefreshTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task PendingRefresh_BlocksMutationCommandsAndPreservesLatestSelection() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new AutomationListProbeRepository(fixture.Automations);
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog);
        await fixture.Automations.SaveAsync(AutomationListOwnershipTests.Schedule(fixture), fixture.Token);
        await fixture.Automations.SaveAsync(AutomationListOwnershipTests.Schedule(fixture) with { Name = "另一个计划" }, fixture.Token);
        await page.ReloadAsync();
        var schedules = await fixture.Automations.GetAllAsync(fixture.Token);
        var completion = new TaskCompletionSource<IReadOnlyList<AutomationSchedule>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.Read = _ => completion.Task;
        var pending = page.ReloadAsync();
        try
        {
            var selected = page.Items.Last();
            page.SelectedItem = selected;
            Assert.False(page.CreateCommand.CanExecute(null));
            Assert.False(page.EditCommand.CanExecute(selected));
            Assert.False(page.ToggleCommand.CanExecute(selected));
            Assert.False(page.RemoveCommand.CanExecute(selected));
        }
        finally { completion.TrySetResult(schedules); await pending; }
        Assert.Equal(schedules.Last().Id, page.SelectedItem!.Schedule.Id);
        Assert.True(page.CreateCommand.CanExecute(null));
        Assert.Equal(0, repository.WriteCount);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RepeatedPendingRefreshes_ShareOneReadInsteadOfQueuingCopies() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new AutomationListProbeRepository(fixture.Automations);
        var completion = new TaskCompletionSource<IReadOnlyList<AutomationSchedule>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.Read = _ => completion.Task;
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog);
        var first = page.ReloadAsync();
        var second = page.ReloadAsync();
        var third = page.ReloadAsync();
        completion.SetResult([]);
        await Task.WhenAll(first, second, third);
        Assert.Equal(1, repository.ReadCount);
        Assert.True(page.IsEmpty);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SuccessfulRetry_ClearsThePreviousReadFailure() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new AutomationListProbeRepository(fixture.Automations)
        { Read = _ => throw new InvalidOperationException("untrusted-secret-marker") };
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog);
        await page.ReloadAsync();
        Assert.DoesNotContain("untrusted-secret-marker", page.Error ?? string.Empty);
        repository.Read = _ => Task.FromResult<IReadOnlyList<AutomationSchedule>>([]);
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.False(page.HasError);
        Assert.True(page.IsEmpty);
    }, TestContext.Current.CancellationToken);
}
