using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspacePreparationMutationOwnershipTests
{
    [Fact]
    public async Task ShutdownWaitsForAnUncooperativeAttachmentWriteAndNeverPersistsOrSendsItsLateResult()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = CancellationToken.None;
        var store = new RecordingConversationAttachmentStore
        {
            SaveHandler = async token =>
            {
                observed = token;
                entered.TrySetResult();
                await release.Task;
                return [];
            }
        };
        var repository = new TestWorkspaceRepository();
        using var runs = new WorkspaceRunTracker();
        await using var workspace = fixture.CreateWorkspace(repository, runs, attachmentStore: store);
        await workspace.LoadAsync();
        workspace.ComposerText = "等待附件写入安全返回。";
        var project = workspace.Catalog.SelectedProject!;
        var attachment = new WorkspaceAttachmentViewModel(new WorkspaceAttachmentContent(
            Path.Combine(project.PrimaryDirectory, "retained.txt"), "retained.txt", "synthetic content", 17), _ => { },
            authorizationStamp: WorkspaceAttachmentAuthorization.Create(project));
        workspace.Attachments.Items.Add(attachment);
        Task stopping;
        try
        {
            workspace.PrimaryActionCommand.Execute(null);
            await entered.Task.WaitAsync(fixture.Token);
            stopping = workspace.ShutdownAsync();
            Assert.True(observed.IsCancellationRequested);
            await Assert.ThrowsAsync<TimeoutException>(() => stopping.WaitAsync(TimeSpan.FromMilliseconds(100), fixture.Token));
            Assert.True(workspace.IsRunning);
            Assert.True(workspace.Catalog.IsLocked);
            Assert.False(workspace.PrimaryActionCommand.CanExecute(null));
        }
        finally { release.TrySetResult(); }
        await stopping.WaitAsync(fixture.Token);
        Assert.False(workspace.IsRunning);
        Assert.Equal("等待附件写入安全返回。", workspace.ComposerText);
        Assert.Same(attachment, Assert.Single(workspace.Attachments.Items));
        Assert.Null(repository.SavedTask);
        Assert.Empty(repository.Messages);
        Assert.Empty(fixture.Server.Requests);
    }

    [Fact]
    public async Task FinalizationDeadlineRequestsCancellationButDoesNotAbandonAnOutstandingStatusWrite()
    {
        var clock = new ManualToolTimeProvider();
        using var preparation = new WorkspaceSendPreparation(clock);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = CancellationToken.None;
        var repository = new TestWorkspaceRepository
        {
            SaveTaskHandler = (_, token) => { observed = token; return release.Task; }
        };
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), null, "fixture", ApprovalPolicy.ReadOnly, AgentTaskStatus.Running, now, now, null);
        var finalizing = preparation.CancelPreparedTaskAsync(new WorkspaceTaskFactory(repository, new WorkspaceWorktreeProbe()), task);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(31));
            Assert.True(observed.IsCancellationRequested);
            await Assert.ThrowsAsync<TimeoutException>(() => finalizing.WaitAsync(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken));
        }
        finally { release.TrySetResult(); }
        var result = await finalizing.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AgentTaskStatus.Cancelled, result.Status);
        Assert.Equal(result, repository.SavedTask);
    }

    [Fact]
    public async Task CleanupDeadlineRequestsCancellationButRetainsOwnershipUntilWorktreeRemovalReturns()
    {
        var clock = new ManualToolTimeProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = CancellationToken.None;
        var worktree = new WorkspaceWorktreeProbe
        {
            RemoveHandler = token => { observed = token; entered.TrySetResult(); return release.Task; }
        };
        var failure = new IOException("synthetic persistence rejection");
        var repository = new TestWorkspaceRepository { SaveTaskHandler = (_, _) => Task.FromException(failure) };
        var factory = new WorkspaceTaskFactory(repository, worktree, timeProvider: clock);
        var project = new ProjectProfile(Guid.NewGuid(), "fixture", Path.GetTempPath(), [], DateTimeOffset.UtcNow);
        var creating = factory.CreateAsync(project, "fixture", ApprovalPolicy.ReadOnly, true, TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromSeconds(31));
            Assert.True(observed.IsCancellationRequested);
            await Assert.ThrowsAsync<TimeoutException>(() => creating.WaitAsync(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken));
        }
        finally { release.TrySetResult(); }
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => creating));
        Assert.Single(worktree.Removed);
        Assert.Null(repository.SavedTask);
    }
}
