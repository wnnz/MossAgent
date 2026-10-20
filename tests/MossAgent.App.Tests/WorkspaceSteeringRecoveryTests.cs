using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceSteeringRecoveryTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task LostAcknowledgementCanBeConfirmedWithoutDuplicateInputOrASpuriousDraftConflict() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await CreateAsync();
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        workspace.ComposerText = "confirm this guidance only once";
        await workspace.Drafts.FlushAsync();
        var probe = Assert.IsType<SteeringAcceptanceProbe>(fixture.Steering);
        probe.AfterAccept = _ => { probe.AfterAccept = null; throw new IOException("synthetic secret in a lost acknowledgement"); };
        await workspace.Steering.SubmitCommand.ExecuteAsync(null);
        Assert.True(workspace.Steering.HasNotice);
        Assert.DoesNotContain("synthetic secret", workspace.Steering.Notice);
        Assert.Equal("confirm this guidance only once", workspace.ComposerText);
        var first = Assert.Single(workspace.Steering.History.Items);
        await workspace.Steering.SubmitCommand.ExecuteAsync(null);
        Assert.Equal(string.Empty, workspace.ComposerText);
        Assert.Equal(first.Id, Assert.Single(workspace.Steering.History.Items).Id);
        Assert.Contains("已确认先前接收", workspace.Steering.Notice);
        workspace.ComposerText = "next independent draft";
        await workspace.Drafts.FlushAsync();
        Assert.False(workspace.Drafts.IsConflict);
        Assert.Equal("next independent draft", (await fixture.Drafts.GetAsync(workspace.Drafts.Scope, fixture.Token))!.Text);
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.Equal(2, fixture.Server.Requests.Count);
        Assert.Single(await fixture.Tasks.GetMessagesAsync(taskId, fixture.Token), message => message.Id == first.Id);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AnotherTaskCanSteerWhileAcceptanceWaitsAndCancellingTheFirstPreservesBothDrafts() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await CreateAsync();
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var taskA = workspace.Catalog.SelectedTask!.Id;
        workspace.ComposerText = "lane A pending acceptance";
        await workspace.Drafts.FlushAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = true;
        Assert.IsType<SteeringAcceptanceProbe>(fixture.Steering).BeforeAccept = async token =>
        { if (first) { first = false; entered.TrySetResult(); await release.Task.WaitAsync(token); } };
        var savingA = workspace.Steering.SubmitCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(fixture.Token);
        Assert.True(workspace.Steering.IsBusy);
        workspace.Catalog.NewChatCommand.Execute(null);
        workspace.ComposerText = "lane B initial";
        await fixture.WaitUntilAsync(() => workspace.PrimaryActionCommand.CanExecute(null));
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => workspace.Catalog.SelectedTask is { } task && task.Id != taskA
            && fixture.Runs.TryGet(task.Id, out var state) && state?.Steering is not null);
        var taskB = workspace.Catalog.SelectedTask!.Id;
        workspace.ComposerText = "lane B accepted guidance";
        await fixture.WaitUntilAsync(() => workspace.Steering.CanSubmit);
        Assert.True(workspace.Steering.SubmitCommand.CanExecute(null));
        await workspace.Steering.SubmitCommand.ExecuteAsync(null);
        Assert.Single(workspace.Steering.History.Items);
        workspace.ComposerText = "lane B foreground draft";
        fixture.Runs.Cancel(taskA);
        await savingA.WaitAsync(fixture.Token);
        await fixture.WaitUntilAsync(() => !fixture.Runs.IsRunning(taskA));
        Assert.True(fixture.Runs.IsRunning(taskB));
        Assert.Equal("lane B foreground draft", workspace.ComposerText);
        Assert.Empty((await fixture.Steering.GetInputsAsync(taskA, long.MaxValue, 64, fixture.Token)).Items);
        Assert.Single((await fixture.Steering.GetInputsAsync(taskB, long.MaxValue, 64, fixture.Token)).Items);
        await workspace.Drafts.FlushAsync();
        Assert.Equal("lane A pending acceptance", (await fixture.Drafts.GetAsync(new(null, taskA), fixture.Token))!.Text);
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !fixture.Runs.IsRunning(taskB));
        Assert.Equal(3, fixture.Server.Requests.Count);
        release.TrySetResult();
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CancelledInputRemainsVisibleAfterWorkspaceReconstructionWithoutAutomaticContinuation() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await CreateAsync();
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        workspace.ComposerText = "inspect interrupted guidance";
        await workspace.Steering.SubmitCommand.ExecuteAsync(null);
        var id = Assert.Single(workspace.Steering.History.Items).Id;
        workspace.ComposerText = "reconstruction draft";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning && !workspace.Steering.History.IsLoading
            && workspace.Steering.History.Items.Single().Status.Contains("已中断", StringComparison.Ordinal));
        await workspace.ShutdownAsync();
        using var runs = new WorkspaceRunTracker();
        await using var restored = fixture.CreateWorkspace(fixture.Tasks, runs, conversationDrafts: fixture.Drafts);
        await restored.LoadAsync();
        await fixture.WaitUntilAsync(() => restored.Catalog.SelectedTask?.Id == taskId && !restored.Steering.History.IsLoading
            && restored.Steering.History.Items.Count == 1);
        Assert.Equal(id, restored.Steering.History.Items[0].Id);
        Assert.Contains("已中断", restored.Steering.History.Items[0].Status);
        Assert.Equal("inspect interrupted guidance", restored.Steering.History.Items[0].Text);
        Assert.Equal("reconstruction draft", restored.ComposerText);
        Assert.False(restored.Steering.SubmitCommand.CanExecute(null));
        Assert.False(restored.HasActiveRun);
        Assert.Single(fixture.Server.Requests);
        await runs.StopAsync();
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task FullMailboxRefusalKeepsTheNextDraftAndDoesNotQueueOrReplayIt() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await CreateAsync();
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        for (var index = 0; index < ConversationSteeringInput.MaximumPendingItems; index++)
        {
            workspace.ComposerText = $"bounded guidance {index}";
            await workspace.Steering.SubmitCommand.ExecuteAsync(null);
            Assert.Equal(string.Empty, workspace.ComposerText);
        }
        workspace.ComposerText = "preserved over-capacity guidance";
        await workspace.Steering.SubmitCommand.ExecuteAsync(null);
        Assert.Equal("preserved over-capacity guidance", workspace.ComposerText);
        Assert.Contains("容量已满", workspace.Steering.Notice);
        Assert.Equal(16, workspace.Steering.History.Items.Count);
        Assert.Empty(workspace.Queue.Items);
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.Equal(2, fixture.Server.Requests.Count);
        Assert.Equal(16, (await fixture.Steering.GetInputsAsync(taskId, long.MaxValue, 64, fixture.Token)).Items.Count);
        Assert.Equal("preserved over-capacity guidance", workspace.ComposerText);
    }, TestContext.Current.CancellationToken);

    private static Task<WorkspaceModelSelectionFixture> CreateAsync() => WorkspaceModelSelectionFixture.CreateAsync(
        pauseResponse: true, withProject: false, configure: services =>
        {
            services.AddSingleton<SqliteConversationSteeringRepository>();
            services.AddSingleton<IConversationSteeringRepository>(provider => new SteeringAcceptanceProbe(
                provider.GetRequiredService<SqliteConversationSteeringRepository>()));
        });
}
