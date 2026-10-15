using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using NativeWebView.Platform.Windows;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class EmbeddedBrowserPaneActionTests
{
    [Fact]
    public void BlankTabAttachment_ClearsPreviousAddress()
    {
        using var view = new AppEmbeddedBrowserView(new WindowsNativeWebViewBackend());
        var pane = new EmbeddedBrowserPaneViewModel { Address = "https://previous.invalid/page" };
        pane.Attach(view);

        Assert.Empty(pane.Address);
        Assert.Same(view, pane.Content);
        Assert.False(pane.NavigateCommand.CanExecute(null));
        pane.Detach(view);
    }

    [Fact]
    public async Task ManualNavigation_UsesAttachedSessionInsteadOfNativeView()
    {
        using var view = new AppEmbeddedBrowserView(new WindowsNativeWebViewBackend());
        using var fixture = new BrowserToolbarFixture();
        var pane = new EmbeddedBrowserPaneViewModel(executor: fixture.Executor, taskId: fixture.Context.TaskId);
        pane.AttachContext(fixture.Context);
        var session = new DelayedBrowserSession();
        pane.Attach(view);
        pane.AttachSession(session);
        session.Requests.Dequeue().SetResult([]);
        pane.Address = "http://127.0.0.1:8080/local-fixture";
        Assert.True(pane.NavigateCommand.CanExecute(null));
        await pane.NavigateCommand.ExecuteAsync(null);

        Assert.Equal(new Uri(pane.Address), session.CurrentUri);
        pane.DetachSession(session);
        pane.Detach(view);
    }

    [Fact]
    public async Task ManualOperationFailure_UsesActionableSafeStatus()
    {
        using var fixture = new BrowserToolbarFixture();
        var pane = new EmbeddedBrowserPaneViewModel(executor: fixture.Executor, taskId: fixture.Context.TaskId);
        pane.AttachContext(fixture.Context);
        var session = new DelayedBrowserSession();
        pane.AttachSession(session);
        session.Requests.Dequeue().SetResult([]);
        await pane.NewTabCommand.ExecuteAsync(null);

        Assert.Equal("浏览器操作失败，请检查会话状态后重试。", pane.Status);
        pane.DetachSession(session);
    }
}
