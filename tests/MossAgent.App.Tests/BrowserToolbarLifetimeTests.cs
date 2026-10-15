using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class BrowserToolbarLifetimeTests
{
    [Fact]
    public async Task CancelDuringApproval_DoesNotExecuteAndCompletesAudit()
    {
        using var fixture = new BrowserToolbarFixture(ApprovalPolicy.AskEveryTime);
        var approval = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Approve = token => approval.Task.WaitAsync(token);
        var pending = fixture.RunAsync("reload");
        Assert.True(fixture.Pane.Actions.IsBusy);
        Assert.False(fixture.Pane.NewTabCommand.CanExecute(null));
        fixture.Pane.Actions.CancelCommand.Execute(null);
        await pending;

        Assert.Empty(fixture.Session.Calls);
        Assert.Equal("tool_cancelled", Assert.Single(fixture.Audit.Completions).ErrorCode);
        Assert.Equal("浏览器操作已取消。", fixture.Pane.Status);
        Assert.True(fixture.Pane.ReloadCommand.CanExecute(null));
    }

    [Fact]
    public async Task CancelNonCooperativeOperation_RetainsOwnershipUntilItReturns()
    {
        using var fixture = new BrowserToolbarFixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken executionToken = default;
        fixture.Session.Perform = (_, token) => { executionToken = token; return release.Task; };
        var pending = fixture.RunAsync("reload");
        fixture.Pane.Actions.CancelCommand.Execute(null);

        Assert.True(executionToken.IsCancellationRequested);
        Assert.True(fixture.Pane.Actions.IsBusy);
        Assert.False(fixture.Pane.Actions.CancelCommand.CanExecute(null));
        await fixture.RunAsync("new_tab");
        Assert.Single(fixture.Session.Calls);
        Assert.Empty(fixture.Audit.Completions);
        release.SetResult();
        await pending;

        Assert.False(fixture.Pane.Actions.IsBusy);
        Assert.Equal("tool_cancelled", Assert.Single(fixture.Audit.Completions).ErrorCode);
        Assert.Equal("浏览器操作已取消。", fixture.Pane.Status);
    }

    [Fact]
    public async Task ApprovalDeadline_CancelsWaitingWithoutSideEffects()
    {
        using var fixture = new BrowserToolbarFixture(ApprovalPolicy.AskEveryTime, TimeSpan.FromMilliseconds(100));
        var approval = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Approve = token => approval.Task.WaitAsync(token);
        await fixture.RunAsync("reload");

        Assert.Empty(fixture.Session.Calls);
        Assert.False(fixture.Pane.Actions.IsBusy);
        Assert.Equal("浏览器操作超时，请检查审批或会话状态后重试。", fixture.Pane.Status);
        Assert.Single(fixture.Audit.Completions);
    }

    [Fact]
    public async Task ExecutionDeadline_RetainsBusyUntilNonCooperativeReturn()
    {
        using var fixture = new BrowserToolbarFixture(timeout: TimeSpan.FromMilliseconds(100));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Session.Perform = async (_, token) =>
        {
            using var registration = token.Register(() => cancelled.TrySetResult());
            await release.Task;
        };
        var pending = fixture.RunAsync("reload");
        await cancelled.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(fixture.Pane.Actions.IsBusy);
        Assert.False(fixture.Pane.ReloadCommand.CanExecute(null));
        release.SetResult();
        await pending;

        Assert.Equal("浏览器操作超时，请检查审批或会话状态后重试。", fixture.Pane.Status);
    }

    [Fact]
    public async Task CommandCancellation_PropagatesToSession()
    {
        using var fixture = new BrowserToolbarFixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Session.Perform = (_, token) => release.Task.WaitAsync(token);
        var pending = fixture.RunAsync("reload");
        fixture.Pane.ReloadCommand.Cancel();
        await pending;

        Assert.Equal("浏览器操作已取消。", fixture.Pane.Status);
        Assert.Equal("tool_cancelled", Assert.Single(fixture.Audit.Completions).ErrorCode);
    }

    [Fact]
    public async Task SessionReplacement_CancelsOldWorkAndRejectsStaleFailure()
    {
        using var fixture = new BrowserToolbarFixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Session.Perform = (_, _) => release.Task;
        var pending = fixture.RunAsync("reload");
        fixture.Pane.AttachSession(new BrowserToolbarSession());
        var status = fixture.Pane.Status;
        Assert.True(fixture.Pane.Actions.IsBusy);
        Assert.False(fixture.Pane.ReloadCommand.CanExecute(null));
        release.SetException(new InvalidOperationException("stale failure"));
        await pending;

        Assert.Equal(status, fixture.Pane.Status);
        Assert.True(fixture.Pane.ReloadCommand.CanExecute(null));
    }

    [Fact]
    public async Task Disposal_CancelsOwnedOperationAndDisablesAllCommands()
    {
        using var fixture = new BrowserToolbarFixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Session.Perform = (_, token) => release.Task.WaitAsync(token);
        var pending = fixture.RunAsync("reload");
        fixture.Pane.Dispose();
        await pending;

        Assert.False(fixture.Pane.Actions.CanExecute);
        Assert.False(fixture.Pane.NewTabCommand.CanExecute(null));
        Assert.False(fixture.Pane.ReloadCommand.CanExecute(null));
        await fixture.RunAsync("new_tab");
        Assert.Single(fixture.Session.Calls);
    }
}
