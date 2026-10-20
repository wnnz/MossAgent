using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceFollowupProcessOwnershipTests
{
    [Fact]
    public async Task CancellingOneQueuedCommandStopsOnlyItsProcessTreeAndPausesOnlyItsQueue()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        var workspace = fixture.Workspace;
        var root = workspace.Catalog.SelectedProject!.PrimaryDirectory;
        using var probe = new QueuedProcessOwnershipProbe(root);
        await probe.CreateAsync("first", fixture.Token);
        await probe.CreateAsync("second", fixture.Token);
        fixture.Server.ResponseOverride = QueuedProcessOwnershipProbe.Response;
        workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.AskEveryTime);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var first = workspace.Catalog.SelectedTask!;
        foreach (var prompt in new[] { "first-owned-process", "first-after-cancellation" })
        {
            workspace.ComposerText = prompt;
            await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        }
        workspace.ComposerText = "first-unsent-draft";
        workspace.Catalog.NewTaskCommand.Execute(null);
        await workspace.Drafts.LoadCurrentAsync();
        workspace.ComposerText = "second-initial";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => workspace.HasActiveRun && workspace.Catalog.SelectedTask?.Id != first.Id);
        var second = workspace.Catalog.SelectedTask!;
        workspace.ComposerText = "second-owned-process";
        await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        workspace.ComposerText = "second-unsent-draft";
        fixture.Server.ReleaseResponses();
        try
        {
            await ApproveBothAsync(fixture);
            var firstParent = await probe.BindAsync("first-parent", fixture.Token);
            var firstChild = await probe.BindAsync("first-child", fixture.Token);
            var secondParent = await probe.BindAsync("second-parent", fixture.Token);
            var secondChild = await probe.BindAsync("second-child", fixture.Token);
            workspace.Catalog.SelectTaskCommand.Execute(first);
            await workspace.Drafts.LoadCurrentAsync();
            workspace.PrimaryActionCommand.Execute(null);
            await firstParent.WaitForExitAsync(fixture.Token);
            await firstChild.WaitForExitAsync(fixture.Token);
            await fixture.WaitUntilAsync(() => !fixture.Runs.IsRunning(first.Id) && workspace.Queue.HasNotice && workspace.Queue.Items.Count == 1);
            Assert.False(secondParent.HasExited);
            Assert.False(secondChild.HasExited);
            Assert.True(fixture.Runs.IsRunning(second.Id));
            Assert.Equal("first-unsent-draft", workspace.ComposerText);
            await probe.ReleaseAsync("second", fixture.Token);
            await fixture.WaitUntilAsync(() => !fixture.Runs.IsRunning(second.Id));
            await secondParent.WaitForExitAsync(fixture.Token);
            await secondChild.WaitForExitAsync(fixture.Token);
            await AssertResultsAsync(fixture, first.Id, second.Id, root);
            await workspace.Queue.ContinueCommand.ExecuteAsync(null);
            await fixture.WaitUntilAsync(() => !workspace.IsRunning && workspace.Queue.Items.Count == 0);
            Assert.Single(await fixture.ToolAudits.GetRecentAsync(first.Id, 10, fixture.Token));
            Assert.Equal("first-unsent-draft", workspace.ComposerText);
        }
        finally { await workspace.ShutdownAsync(); }
    }

    private static async Task ApproveBothAsync(WorkspaceModelSelectionFixture fixture)
    {
        var approval = fixture.Main.Approval;
        for (var index = 0; index < 2; index++)
        {
            await fixture.WaitUntilAsync(() => approval.IsPending && approval.ApproveCommand.CanExecute(approval.CurrentRequestId));
            Assert.Equal("shell.run", approval.ToolName);
            approval.ApproveCommand.Execute(approval.CurrentRequestId);
        }
    }

    private static async Task AssertResultsAsync(WorkspaceModelSelectionFixture fixture, Guid first, Guid second, string root)
    {
        var failed = Assert.Single(await fixture.ToolAudits.GetRecentAsync(first, 10, fixture.Token));
        var success = Assert.Single(await fixture.ToolAudits.GetRecentAsync(second, 10, fixture.Token));
        Assert.Equal("tool_cancelled", failed.ErrorCode);
        Assert.Equal(ApprovalPolicy.AskEveryTime, failed.ApprovalPolicy);
        Assert.True(failed.ApprovalGranted);
        Assert.True(success.IsSuccess);
        Assert.True(success.ApprovalGranted);
        Assert.Equal(ApprovalPolicy.AskEveryTime, success.ApprovalPolicy);
        Assert.False(File.Exists(Path.Combine(root, "first-child.done")));
        Assert.True(File.Exists(Path.Combine(root, "second-child.done")));
        var workspace = fixture.Workspace;
        var selected = workspace.Catalog.SelectedTask!;
        workspace.Catalog.SelectTaskCommand.Execute(workspace.Catalog.FindTask(selected.ProjectId, second));
        await workspace.Drafts.LoadCurrentAsync();
        Assert.Equal("second-unsent-draft", workspace.ComposerText);
        Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask?.Status);
        Assert.True(Assert.Single(workspace.Messages, message => message.ToolCallId == "second-process").Tool!.IsSuccess);
        workspace.Catalog.SelectTaskCommand.Execute(workspace.Catalog.FindTask(selected.ProjectId, first));
        await workspace.Drafts.LoadCurrentAsync();
        await workspace.Queue.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(AgentTaskStatus.Cancelled, workspace.Catalog.SelectedTask?.Status);
    }
}
