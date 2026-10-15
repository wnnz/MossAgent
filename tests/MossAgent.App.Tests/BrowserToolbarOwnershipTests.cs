using MossAgent.App.ViewModels;
using MossAgent.Browser;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class BrowserToolbarOwnershipTests
{
    [Fact]
    public async Task MissingContextOrExecutor_DisablesDirectCommandExecution()
    {
        using var fixture = new BrowserToolbarFixture();
        fixture.Pane.AttachContext(null);
        await fixture.RunAsync("reload");
        Assert.False(fixture.Pane.ReloadCommand.CanExecute(null));
        Assert.Empty(fixture.Audit.Starts);

        using var pane = new EmbeddedBrowserPaneViewModel(taskId: fixture.Context.TaskId);
        pane.AttachContext(fixture.Context);
        pane.AttachSession(fixture.Session);
        Assert.False(pane.NewTabCommand.CanExecute(null));
        await pane.NewTabCommand.ExecuteAsync(null);
        Assert.Empty(fixture.Session.Calls);
    }

    [Fact]
    public void WrongContext_RevokesAccessInsteadOfBorrowingAnotherTaskPolicy()
    {
        using var fixture = new BrowserToolbarFixture(ApprovalPolicy.ReadOnly);
        var foreign = fixture.Context with { TaskId = Guid.NewGuid(), ApprovalPolicy = ApprovalPolicy.FullAccess };
        Assert.Throws<ArgumentException>(() => fixture.Pane.AttachContext(foreign));
        Assert.False(fixture.Pane.Actions.CanExecute);
        Assert.Empty(fixture.Session.Calls);
    }

    [Fact]
    public async Task LazyContextSession_CannotRedirectManualActions()
    {
        using var fixture = new BrowserToolbarFixture(ApprovalPolicy.AskEveryTime);
        var creations = 0;
        await using var lazy = new LazyBrowserSession(_ =>
        {
            creations++;
            throw new InvalidOperationException("Do not create a different browser.");
        });
        fixture.Pane.AttachContext(fixture.Context with { BrowserSession = lazy });
        await fixture.RunAsync("reload");

        Assert.Equal(0, creations);
        Assert.Same(fixture.Session, Assert.Single(fixture.ApprovalContexts).BrowserSession);
        Assert.Equal("reload", Assert.Single(fixture.Session.Calls));
    }

    [Fact]
    public async Task TaskSwitch_CancelsHiddenOperationWithoutBlockingSelectedTask()
    {
        using var firstFixture = new BrowserToolbarFixture();
        using var secondFixture = new BrowserToolbarFixture();
        var workspace = new EmbeddedBrowserWorkspaceViewModel(firstFixture.Executor);
        workspace.SelectTask(firstFixture.Context.TaskId, firstFixture.Context);
        var first = workspace.Open(firstFixture.Context.TaskId);
        using var firstPane = first;
        first.AttachSession(firstFixture.Session);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken oldToken = default;
        firstFixture.Session.Perform = (_, token) => { oldToken = token; return release.Task; };
        var pending = first.NewTabCommand.ExecuteAsync(null);
        var second = workspace.Open(secondFixture.Context.TaskId);
        using var secondPane = second;
        second.AttachSession(secondFixture.Session);
        workspace.SelectTask(secondFixture.Context.TaskId, secondFixture.Context);

        Assert.True(oldToken.IsCancellationRequested);
        Assert.Same(second, workspace.CurrentPane);
        Assert.True(first.Actions.IsBusy);
        Assert.False(first.NewTabCommand.CanExecute(null));
        await second.NewTabCommand.ExecuteAsync(null);
        Assert.Equal("new_tab", Assert.Single(secondFixture.Session.Calls));
        release.SetResult();
        await pending;

        Assert.Equal("浏览器操作已完成。", second.Status);
        Assert.False(first.NewTabCommand.CanExecute(null));
        workspace.SelectTask(firstFixture.Context.TaskId, firstFixture.Context);
        Assert.True(first.NewTabCommand.CanExecute(null));
        Assert.DoesNotContain("正在执行", first.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void WrongWorkspaceContext_DoesNotChangeSelection()
    {
        using var fixture = new BrowserToolbarFixture();
        var workspace = new EmbeddedBrowserWorkspaceViewModel(fixture.Executor);
        workspace.SelectTask(fixture.Context.TaskId, fixture.Context);
        using var pane = workspace.Open(fixture.Context.TaskId);
        Assert.Throws<ArgumentException>(() => workspace.SelectTask(Guid.NewGuid(), fixture.Context));
        Assert.Same(pane, workspace.CurrentPane);
    }

    [Fact]
    public async Task SameOwnerContextChange_CancelsWithoutPublishingStaleResult()
    {
        using var fixture = new BrowserToolbarFixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Session.Perform = (_, _) => release.Task;
        var pending = fixture.RunAsync("reload");
        fixture.Pane.AttachContext(fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly });
        var status = fixture.Pane.Status;
        release.SetException(new InvalidOperationException("old failure"));
        await pending;
        Assert.Equal(status, fixture.Pane.Status);
        await fixture.RunAsync("reload");

        Assert.Single(fixture.Session.Calls);
        Assert.Equal("capability_denied", fixture.Audit.Completions.Last().ErrorCode);
    }
}
