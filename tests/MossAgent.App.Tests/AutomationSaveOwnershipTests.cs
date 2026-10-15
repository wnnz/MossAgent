using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationSaveOwnershipTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task CancelledSave_PropagatesCancellationButRetainsNoncooperativeOwnership() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var token = CancellationToken.None;
        var repository = new AutomationListProbeRepository(fixture.Automations)
        { Write = (_, cancellation) => { token = cancellation; return completion.Task; } };
        using var page = Page(fixture, repository);
        await OpenNewAsync(page, fixture);
        var pending = page.SaveCommand.ExecuteAsync(null);
        try
        {
            Assert.True(page.SaveCommand.CanBeCanceled);
            page.SaveCommand.Cancel();
            Assert.True(token.IsCancellationRequested);
            Assert.True(page.IsBusy);
            Assert.False(page.CloseEditorCommand.CanExecute(null));
            Assert.False(pending.IsCompleted);
        }
        finally { completion.TrySetResult(false); await pending; }
        Assert.True(page.Editor.IsOpen);
        Assert.Empty(await fixture.Automations.GetAllAsync(fixture.Token));
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CompletedEarlierDraft_RetainsNewerDraftAndRebasesStoredIdentity() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new AutomationListProbeRepository(fixture.Automations)
        { Write = async (schedule, token) => { await completion.Task; return await fixture.Automations.SaveAsync(schedule, token); } };
        using var page = Page(fixture, repository);
        await OpenNewAsync(page, fixture);
        var pending = page.SaveCommand.ExecuteAsync(null);
        page.Editor.Name = "保存期间形成的新草稿";
        completion.SetResult();
        await pending;
        Assert.True(page.Editor.IsOpen);
        Assert.Equal("保存期间形成的新草稿", page.Editor.Name);
        var original = Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token));
        Assert.Equal(original, page.Editor.Original);
        repository.Write = null;
        await page.SaveCommand.ExecuteAsync(null);
        var saved = Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token));
        Assert.Equal(original.Id, saved.Id);
        Assert.Equal("保存期间形成的新草稿", saved.Name);
        Assert.Equal(original.Revision + 1, saved.Revision);
        Assert.False(saved.IsEnabled);
        Assert.False(page.Editor.IsOpen);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task DisposedLateSave_DoesNotCloseDraftPublishErrorsOrReload(bool result) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new AutomationListProbeRepository(fixture.Automations) { Write = (_, _) => completion.Task };
        using var page = Page(fixture, repository);
        await OpenNewAsync(page, fixture);
        var pending = page.SaveCommand.ExecuteAsync(null);
        var reads = repository.ReadCount;
        page.Dispose();
        completion.SetResult(result);
        await pending;
        Assert.True(page.Editor.IsOpen);
        Assert.False(page.HasError);
        Assert.Equal(reads, repository.ReadCount);
        Assert.False(page.SaveCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SaveOwnsBusyStateThroughItsCatalogReload() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var completion = new TaskCompletionSource<IReadOnlyList<AutomationSchedule>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new AutomationListProbeRepository(fixture.Automations) { Read = _ => completion.Task };
        using var page = Page(fixture, repository);
        await OpenNewAsync(page, fixture);
        var pending = page.SaveCommand.ExecuteAsync(null);
        await fixture.WaitUntilAsync(() => repository.ReadCount > 0);
        try { Assert.True(page.IsBusy); Assert.False(page.CreateCommand.CanExecute(null)); }
        finally { completion.TrySetResult(await fixture.Automations.GetAllAsync(fixture.Token)); await pending; }
        Assert.False(page.IsBusy);
        Assert.False(page.Editor.IsOpen);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SupersededEditor_LateSaveCannotCloseReplacementDraft() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new AutomationListProbeRepository(fixture.Automations)
        { Write = async (schedule, token) => { await completion.Task; return await fixture.Automations.SaveAsync(schedule, token); } };
        using var page = Page(fixture, repository);
        await OpenNewAsync(page, fixture);
        var pending = page.SaveCommand.ExecuteAsync(null);
        page.Editor.Open(null, page.Editor.Projects, page.Editor.Models);
        page.Editor.Name = "另一个配置上下文";
        completion.SetResult();
        await pending;
        Assert.True(page.Editor.IsOpen);
        Assert.Null(page.Editor.Original);
        Assert.Equal("另一个配置上下文", page.Editor.Name);
        Assert.False(page.HasError);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task PendingSave_DirectRejectedReentryKeepsOriginalExecutionTask() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new AutomationListProbeRepository(fixture.Automations) { Write = (_, _) => completion.Task };
        using var page = Page(fixture, repository);
        await OpenNewAsync(page, fixture);
        var pending = page.SaveCommand.ExecuteAsync(null);
        try
        {
            var original = page.SaveCommand.ExecutionTask;
            await page.SaveCommand.ExecuteAsync(null);
            page.SaveCommand.Execute(null);
            Assert.Same(original, page.SaveCommand.ExecutionTask);
            Assert.Equal(1, repository.WriteCount);
            Assert.True(page.IsBusy);
        }
        finally { completion.TrySetResult(false); await pending; }
    }, TestContext.Current.CancellationToken);

    internal static AutomationViewModel Page(WorkspaceModelSelectionFixture fixture, AutomationListProbeRepository repository) =>
        new(repository, fixture.Scheduler, fixture.Configurations, fixture.Tasks, fixture.Workspace.Catalog);

    internal static async Task OpenNewAsync(AutomationViewModel page, WorkspaceModelSelectionFixture fixture)
    {
        await page.CreateCommand.ExecuteAsync(null);
        page.Editor.Name = "正在保存的暂停计划";
        page.Editor.Prompt = "仅审查，不修改代码。";
        page.Editor.SelectedProject = fixture.Workspace.Catalog.SelectedProject;
        page.Editor.SelectedModel = Assert.Single(page.Editor.Models, model => model.Model.Id == fixture.SelectedModel.Id);
    }
}
