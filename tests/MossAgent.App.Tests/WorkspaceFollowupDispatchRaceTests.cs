using System.Collections.Concurrent;
using Avalonia.Headless;
using Avalonia.Threading;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceFollowupDispatchRaceTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task ConfigurationRevokedBeforeClaimKeepsInputQueuedWithoutProviderRequest(bool changeProject, bool beforeClaim) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: changeProject);
        var gate = new FollowupPreparationAttachmentGate(fixture.AttachmentStore);
        var probe = new FollowupDispatchProbe(fixture.Followups);
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.ShutdownAsync();
        using var runs = new WorkspaceRunTracker();
        await using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, conversationDrafts: fixture.Drafts,
            followups: fixture.FollowupServices(probe, beforeClaim ? null : gate));
        var stateChanges = new ConcurrentQueue<bool>();
        workspace.Queue.Items.CollectionChanged += (_, _) => stateChanges.Enqueue(Dispatcher.UIThread.CheckAccess());
        workspace.Queue.PropertyChanged += (_, _) => stateChanges.Enqueue(Dispatcher.UIThread.CheckAccess());
        workspace.PropertyChanged += (_, _) => stateChanges.Enqueue(Dispatcher.UIThread.CheckAccess());
        await workspace.LoadAsync();
        await workspace.Drafts.LoadCurrentAsync();
        workspace.ComposerText = "initial";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        workspace.ComposerText = "revoked-before-queue-claim";
        await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        var itemId = Assert.Single(workspace.Queue.Items).Id;
        var task = workspace.Catalog.SelectedTask!;
        var originalProject = workspace.Catalog.SelectedProject;
        workspace.ComposerText = "keep-next-draft";
        if (beforeClaim) probe.Dispatching = () => ChangeAsync(fixture, workspace, changeProject);
        fixture.Server.ReleaseResponses();
        try
        {
            if (!beforeClaim)
            {
                await gate.Entered.WaitAsync(fixture.Token);
                await ChangeAsync(fixture, workspace, changeProject);
            }
        }
        finally { gate.Release(); }
        await fixture.WaitUntilAsync(() => !workspace.IsRunning
            && (workspace.Queue.HasNotice || fixture.Server.Requests.Count > 1));
        Assert.Single(fixture.Server.Requests.Select((_, index) => index));
        Assert.Equal(itemId, Assert.Single((await fixture.Followups.GetAsync(task.Id, fixture.Token)).Items).Id);
        Assert.DoesNotContain(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token), message => message.Id == itemId);
        Assert.Equal("keep-next-draft", workspace.ComposerText);
        Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask?.Status);
        Assert.Contains(changeProject ? "授权目录已变化" : "模型或提供商不可用", workspace.Queue.Notice);
        probe.Dispatching = null;
        if (changeProject)
        {
            var current = Assert.Single(await fixture.Tasks.GetProjectsAsync(fixture.Token), project => project.Id == originalProject!.Id);
            await fixture.Tasks.SaveProjectAsync(originalProject! with { AuthorizationRevision = current.AuthorizationRevision }, fixture.Token);
        }
        else await fixture.SetSelectedModelEnabledAsync(true);
        await workspace.Queue.ContinueCommand.ExecuteAsync(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning && fixture.Server.Requests.Count == 2
            && workspace.Queue.Items.Count == 0);
        Assert.Single(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token), message => message.Id == itemId);
        Assert.Equal("keep-next-draft", workspace.ComposerText);
        await runs.StopAsync();
        Assert.NotEmpty(stateChanges);
        Assert.All(stateChanges, onUiThread => Assert.True(onUiThread));
    }, TestContext.Current.CancellationToken);

    private static async Task ChangeAsync(WorkspaceModelSelectionFixture fixture, WorkspaceViewModel workspace, bool changeProject)
    {
        if (changeProject)
        {
            var project = workspace.Catalog.SelectedProject!;
            await fixture.Tasks.SaveProjectAsync(project with
            { PrimaryDirectory = fixture.DataRoot, AuthorizedDirectories = [fixture.DataRoot] }, fixture.Token);
        }
        else await fixture.SetSelectedModelEnabledAsync(false);
    }
}
