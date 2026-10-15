using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationSaveRecoveryTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task TimedOutNoncooperativeSave_HoldsOwnershipUntilReturnAndDoesNotRetry() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var clock = new AutomationDeadlineTestClock();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var token = CancellationToken.None;
        var repository = new AutomationListProbeRepository(fixture.Automations)
        { Write = (_, cancellation) => { token = cancellation; return completion.Task; } };
        using var page = new AutomationViewModel(repository, fixture.Scheduler, fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog, clock);
        await AutomationSaveOwnershipTests.OpenNewAsync(page, fixture);
        var pending = page.SaveCommand.ExecuteAsync(null);
        clock.Expire();
        try
        {
            Assert.Equal(TimeSpan.FromSeconds(30), clock.LastDueTime);
            Assert.True(token.IsCancellationRequested);
            Assert.True(page.IsBusy);
            Assert.False(pending.IsCompleted);
            await page.SaveCommand.ExecuteAsync(null);
            Assert.Equal(1, repository.WriteCount);
        }
        finally { completion.TrySetResult(false); await pending; }
        Assert.Equal(AutomationText.Get("OperationTimedOut"), page.Error);
        Assert.True(page.Editor.IsOpen);
        Assert.Empty(await fixture.Automations.GetAllAsync(fixture.Token));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CancelledNoncooperativeCommit_RebasesIdentityWithoutDiscardingDraft() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new AutomationListProbeRepository(fixture.Automations)
        { Write = async (schedule, _) => { await completion.Task; return await fixture.Automations.SaveAsync(schedule, fixture.Token); } };
        using var page = AutomationSaveOwnershipTests.Page(fixture, repository);
        await AutomationSaveOwnershipTests.OpenNewAsync(page, fixture);
        var pending = page.SaveCommand.ExecuteAsync(null);
        page.SaveCommand.Cancel();
        completion.SetResult();
        await pending;
        Assert.True(page.Editor.IsOpen);
        Assert.Equal(AutomationText.Get("OperationCancelled"), page.Error);
        Assert.Equal(AutomationText.Get("CancelledWriteCompleted"), page.Notice);
        var first = Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token));
        Assert.Equal(first, page.Editor.Original);
        repository.Write = null;
        page.Editor.Name = "确认后继续保存";
        await page.SaveCommand.ExecuteAsync(null);
        var second = Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token));
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Revision + 1, second.Revision);
        Assert.False(second.IsEnabled);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CommittedSaveWithFailedReload_KeepsDraftUntilSuccessfulReadConfirmsIt() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new AutomationListProbeRepository(fixture.Automations)
        { Read = _ => throw new IOException("untrusted-secret-marker") };
        using var page = AutomationSaveOwnershipTests.Page(fixture, repository);
        await AutomationSaveOwnershipTests.OpenNewAsync(page, fixture);
        await page.SaveCommand.ExecuteAsync(null);
        Assert.True(page.Editor.IsOpen);
        Assert.True(page.List.HasLoadError);
        Assert.Equal(AutomationText.Get("SavedRefreshPending"), page.Notice);
        Assert.NotNull(page.Editor.Original);
        Assert.False(page.SaveCommand.CanExecute(null));
        repository.Read = null;
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.False(page.Editor.IsOpen);
        Assert.False(page.HasNotice);
        Assert.Equal(1, repository.WriteCount);
        Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ExternalChangeAfterCommit_CannotBeRebasedOrDuplicatedByAnotherSave() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new AutomationListProbeRepository(fixture.Automations)
        {
            Write = async (schedule, token) =>
            {
                var saved = await fixture.Automations.SaveAsync(schedule, token);
                var current = Assert.Single(await fixture.Automations.GetAllAsync(token));
                await fixture.Automations.SaveAsync(current with { Name = "并发外部修改" }, token);
                return saved;
            }
        };
        using var page = AutomationSaveOwnershipTests.Page(fixture, repository);
        await AutomationSaveOwnershipTests.OpenNewAsync(page, fixture);
        await page.SaveCommand.ExecuteAsync(null);
        Assert.True(page.Editor.IsOpen);
        Assert.Equal(AutomationText.Get("Conflict"), page.Error);
        Assert.Equal(1, page.Editor.Original!.Revision);
        repository.Write = null;
        await page.SaveCommand.ExecuteAsync(null);
        var stored = Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token));
        Assert.Equal(page.Editor.Original.Id, stored.Id);
        Assert.Equal("并发外部修改", stored.Name);
        Assert.Equal(2, stored.Revision);
        Assert.Equal(AutomationText.Get("Conflict"), page.Error);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SnapshotPublication_ReopenedSameValuedEditorRemainsOpen() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new AutomationListProbeRepository(fixture.Automations);
        using var page = AutomationSaveOwnershipTests.Page(fixture, repository);
        await AutomationSaveOwnershipTests.OpenNewAsync(page, fixture);
        var notifications = 0;
        page.Editor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(page.Editor.Original) && ++notifications == 2)
                page.Editor.Open(page.Editor.Original, page.Editor.Projects, page.Editor.Models);
        };
        await page.SaveCommand.ExecuteAsync(null);
        Assert.Equal(2, notifications);
        Assert.True(page.Editor.IsOpen);
        Assert.False(page.HasNotice);
        Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token));
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task IdleOrDisposedCancellation_RejectsDirectInvocationSafely(bool disposed) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new AutomationListProbeRepository(fixture.Automations);
        using var page = AutomationSaveOwnershipTests.Page(fixture, repository);
        if (disposed) page.Dispose();
        Assert.False(page.Operation.CancelCommand.CanExecute(null));
        page.Operation.CancelCommand.Execute(null);
        Assert.False(page.IsBusy);
        Assert.Equal(0, repository.WriteCount);
    }, TestContext.Current.CancellationToken);
}
