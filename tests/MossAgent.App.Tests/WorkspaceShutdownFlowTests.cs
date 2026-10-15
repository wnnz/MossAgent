using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceShutdownFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparationRaceDoesNotInvokeProviderAndPersistenceFailureBlocksGracefulExit(bool rejectFinalization)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new TestWorkspaceRepository
        {
            SaveTaskHandler = (task, _) => task.Status == AgentTaskStatus.Running
                ? release.Task : rejectFinalization
                    ? Task.FromException(new InvalidOperationException("Authorization: Bearer synthetic-secret"))
                    : Task.CompletedTask
        };
        using var runs = new WorkspaceRunTracker();
        using var workspace = fixture.CreateWorkspace(repository, runs);
        await workspace.LoadAsync();
        workspace.ComposerText = "创建任务和退出竞争。";
        workspace.PrimaryActionCommand.Execute(null);
        Assert.Equal(AgentTaskStatus.Running, repository.SavedTask?.Status);

        var shutdown = workspace.ShutdownAsync();
        Assert.False(shutdown.IsCompleted);
        Assert.True(workspace.IsShuttingDown);
        release.SetResult();
        if (rejectFinalization)
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => shutdown.WaitAsync(fixture.Token));
            Assert.DoesNotContain("synthetic-secret", failure.Message + workspace.Activity);
        }
        else await shutdown.WaitAsync(fixture.Token);

        Assert.Empty(fixture.Server.Requests);
        Assert.Equal(AgentTaskStatus.Cancelled, repository.SavedTask?.Status);
        Assert.False(workspace.IsRunning);
        Assert.Throws<ObjectDisposedException>(() => runs.TrackExecution(() => Task.CompletedTask));
    }

    [Fact]
    public async Task ShutdownCancelsPausedModelRequestPersistsStateAndBlocksFurtherSends()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);

        var shutdown = workspace.ShutdownAsync();
        Assert.Same(shutdown, workspace.ShutdownAsync());
        await shutdown.WaitAsync(fixture.Token);

        Assert.True(workspace.IsShuttingDown);
        Assert.False(workspace.IsRunning);
        Assert.True(workspace.Catalog.IsLocked);
        await fixture.AssertPersistedAsync(AgentTaskStatus.Cancelled);
        workspace.ComposerText = "退出后不能发送的草稿。";
        Assert.False(workspace.PrimaryActionCommand.CanExecute(null));
        workspace.PrimaryActionCommand.Execute(null);
        Assert.Equal("退出后不能发送的草稿。", workspace.ComposerText);
        Assert.Single(fixture.Server.Requests);
    }
}
