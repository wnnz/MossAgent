using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAgentReadFlowTests
{
    [Fact]
    public async Task FailedAgentRefresh_BlocksActualRequestsAndPreservesTheDraftUntilRetry()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new TestAgentConfigurationRepository();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, fixture.Runs, repository);
        await workspace.LoadAsync();
        workspace.ComposerText = "blocked-agent-catalog-draft";
        repository.Loader = _ => throw new InvalidOperationException("fixture-secret-error");

        await workspace.ReloadAgentsAsync();
        workspace.PrimaryActionCommand.Execute(null);

        Assert.False(workspace.PrimaryActionCommand.CanExecute(null));
        Assert.Equal("blocked-agent-catalog-draft", workspace.ComposerText);
        Assert.Empty(fixture.Server.Requests);
        Assert.Empty(workspace.Catalog.Tasks);
        repository.Loader = null;
        await workspace.AgentPicker.RefreshCommand.ExecuteAsync(null);
        Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => workspace.Catalog.SelectedTask?.Status == AgentTaskStatus.Completed && !workspace.IsRunning);
        Assert.Single(fixture.Server.Requests);
        await workspace.ShutdownAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AgentLoadingOrReadFailure_DoesNotHideTheRunningTasksStopAction(bool failure)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        var repository = new TestAgentConfigurationRepository();
        using var workspace = fixture.CreateWorkspace(fixture.Tasks, fixture.Runs, repository);
        await workspace.LoadAsync();
        workspace.ComposerText = "actual-paused-request";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var gate = new TaskCompletionSource<IReadOnlyList<AgentProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.Loader = _ => failure ? Task.FromException<IReadOnlyList<AgentProfile>>(
            new InvalidOperationException("fixture-secret-error")) : gate.Task;
        var refresh = workspace.ReloadAgentsAsync();

        Assert.False(workspace.AgentPicker.CanUseSelection);
        Assert.True(workspace.IsRunning);
        Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
        Assert.Equal("停止执行", workspace.PrimaryActionLabel);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);

        Assert.Equal(AgentTaskStatus.Cancelled, workspace.Catalog.SelectedTask?.Status);
        Assert.Single(fixture.Server.Requests);
        gate.TrySetResult([]);
        await refresh;
        await workspace.ShutdownAsync();
    }
}
