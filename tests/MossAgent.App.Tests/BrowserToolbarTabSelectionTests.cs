using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class BrowserToolbarTabSelectionTests
{
    [Theory]
    [InlineData(ApprovalPolicy.ReadOnly)]
    [InlineData(ApprovalPolicy.AskEveryTime)]
    public async Task RejectedSelection_DoesNotCommitFakeActiveTab(ApprovalPolicy policy)
    {
        using var fixture = new BrowserToolbarFixture(policy);
        fixture.Approve = _ => Task.FromResult(false);
        var active = fixture.Pane.SelectedTab;
        fixture.Pane.SelectedTab = fixture.Pane.Tabs.Single(tab => !tab.IsActive);
        if (fixture.Pane.SwitchTabCommand.ExecutionTask is { } pending) await pending;

        Assert.Same(active, fixture.Pane.SelectedTab);
        Assert.Equal(active!.Id, fixture.Session.Tabs.Single(tab => tab.IsActive).Id);
        Assert.Empty(fixture.Session.Calls);
    }

    [Fact]
    public async Task ApprovedSelection_IsCommittedOnlyAfterRealSessionMetadata()
    {
        using var fixture = new BrowserToolbarFixture(ApprovalPolicy.AskEveryTime);
        var approval = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Approve = token => approval.Task.WaitAsync(token);
        var previous = fixture.Pane.SelectedTab;
        var target = fixture.Pane.Tabs.Single(tab => !tab.IsActive);
        fixture.Pane.SelectedTab = target;
        Assert.Same(previous, fixture.Pane.SelectedTab);
        Assert.True(fixture.Pane.Actions.IsBusy);
        approval.SetResult(true);
        await fixture.Pane.SwitchTabCommand.ExecutionTask!;

        Assert.Equal(target.Id, fixture.Pane.SelectedTab!.Id);
        Assert.Equal("switch_tab", Assert.Single(fixture.Session.Calls));
    }

    [Fact]
    public async Task BusyAndForeignTabSelection_CannotLaunchAnotherOperation()
    {
        using var fixture = new BrowserToolbarFixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Session.Perform = (_, token) => release.Task.WaitAsync(token);
        var pending = fixture.RunAsync("reload");
        var active = fixture.Pane.SelectedTab;
        fixture.Pane.SelectedTab = fixture.Pane.Tabs.Single(tab => !tab.IsActive);
        Assert.Same(active, fixture.Pane.SelectedTab);
        fixture.Pane.Actions.CancelCommand.Execute(null);
        await pending;
        await fixture.Pane.SwitchTabCommand.ExecuteAsync(new("foreign", "Foreign", null, false));

        Assert.Single(fixture.Session.Calls);
        Assert.Single(fixture.Audit.Starts);
        Assert.Same(active, fixture.Pane.SelectedTab);
    }
}
