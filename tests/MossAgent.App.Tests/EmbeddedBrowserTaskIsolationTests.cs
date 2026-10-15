using Avalonia;
using MossAgent.App.Browser;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions.Browser;
using NativeWebView.Platform.Windows;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class EmbeddedBrowserTaskIsolationTests
{
    [Fact]
    public void SelectingTasksWithoutSessions_DoesNotCreateNativeViews()
    {
        var workspace = new EmbeddedBrowserWorkspaceViewModel();
        workspace.SelectTask(Guid.NewGuid());
        workspace.SelectTask(null);

        Assert.False(workspace.CurrentPane.HasBrowser);
        Assert.False(workspace.CurrentPane.NavigateCommand.CanExecute(null));
        Assert.Empty(workspace.Host.Children);
    }

    [Fact]
    public async Task BackgroundSession_CannotReplaceSelectedTaskPresentation()
    {
        var workspace = new EmbeddedBrowserWorkspaceViewModel();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        workspace.SelectTask(firstId);
        var first = workspace.Open(firstId);
        await using var firstSession = Session(workspace, firstId, first);
        first.Address = "https://first.invalid/";
        var second = workspace.Open(secondId);
        await using var secondSession = Session(workspace, secondId, second);
        second.Address = "https://second.invalid/";

        Assert.Same(first, workspace.CurrentPane);
        Assert.Equal("https://first.invalid/", workspace.CurrentPane.Address);
        Assert.NotSame(first.Content, second.Content);
        Assert.False(first.NavigateCommand.CanExecute(null));
        Assert.False(second.NavigateCommand.CanExecute(null));
        workspace.SelectTask(secondId);
        Assert.Same(second, workspace.CurrentPane);
        workspace.SelectTask(firstId);
        Assert.Same(first, workspace.CurrentPane);
        Assert.Equal(2, workspace.Host.Children.Count);
    }

    [Fact]
    public async Task ReleasingOneTask_RemovesOnlyItsOwnViewsAndCommands()
    {
        var workspace = new EmbeddedBrowserWorkspaceViewModel();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var first = workspace.Open(firstId);
        await using var firstSession = Session(workspace, firstId, first);
        await firstSession.NewTabAsync(null, TestContext.Current.CancellationToken);
        var second = workspace.Open(secondId);
        await using var secondSession = Session(workspace, secondId, second);
        workspace.SelectTask(secondId);
        Assert.Equal(3, workspace.Host.Children.Count);

        await firstSession.DisposeAsync();

        Assert.Same(second, workspace.CurrentPane);
        Assert.Same(second.Content, Assert.Single(workspace.Host.Children));
        Assert.False(first.HasBrowser);
        Assert.False(first.NavigateCommand.CanExecute(null));
        Assert.True(second.HasBrowser);
        Assert.False(second.NavigateCommand.CanExecute(null));
    }

    [Fact]
    public void StaleRelease_CannotClearReplacementPane()
    {
        var workspace = new EmbeddedBrowserWorkspaceViewModel();
        var taskId = Guid.NewGuid();
        workspace.SelectTask(taskId);
        var previous = workspace.Open(taskId);
        workspace.Close(taskId, previous);
        var replacement = workspace.Open(taskId);

        workspace.Close(taskId, previous);
        workspace.SelectTask(taskId);

        Assert.Same(replacement, workspace.CurrentPane);
        Assert.Throws<InvalidOperationException>(() => workspace.Open(taskId));
        Assert.Throws<ArgumentException>(() => workspace.Open(Guid.Empty));
    }

    [Fact]
    public void HiddenAndBackgroundViews_KeepNonzeroLayoutWithoutInput()
    {
        using var first = View();
        using var second = View();
        var host = new AppBrowserHost();
        host.Register(first);
        host.Register(second);
        var viewport = new AppBrowserViewport();
        host.UpdateViewport(viewport, first, new Rect(600, 20, 277, 640));
        Arrange(host);

        Assert.Equal(new Rect(600, 20, 277, 640), first.Bounds);
        Assert.Equal(first.Bounds.Size, second.Bounds.Size);
        Assert.True(second.Bounds.Right < 0 && second.Bounds.Bottom < 0);
        Assert.True(first.IsHitTestVisible);
        Assert.False(second.IsHitTestVisible);
        Assert.False(second.Focusable);
        host.ClearViewport(viewport);
        Arrange(host);
        Assert.True(first.Bounds.Right < 0);
        Assert.Equal(new Size(277, 640), first.Bounds.Size);
        Assert.False(first.IsHitTestVisible);
        Assert.Equal(2, host.Children.Count);
        host.Unregister(first);
        Assert.Same(second, Assert.Single(host.Children));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task NativeFactory_RejectsUnownedSessions(string? taskId)
    {
        var workspace = new EmbeddedBrowserWorkspaceViewModel();
        var factory = new EmbeddedBrowserSessionFactory(workspace);
        var options = new BrowserLaunchOptions(BrowserEngine.Embedded, BrowserProfileMode.Managed,
            null, null, null, null, null, taskId is null ? null : Guid.Parse(taskId));

        await Assert.ThrowsAsync<ArgumentException>(() => factory.CreateAsync(options,
            TestContext.Current.CancellationToken));
        Assert.Empty(workspace.Host.Children);
        Assert.False(workspace.CurrentPane.HasBrowser);
    }

    [Fact]
    public void InitialBackgroundView_IsArrangedBeforeAnyInspectorIsShown()
    {
        using var view = View();
        var host = new AppBrowserHost();
        host.Register(view);

        Arrange(host);

        Assert.True(view.IsMeasureValid && view.IsArrangeValid);
        Assert.True(view.Bounds.Width > 0 && view.Bounds.Height > 0);
        Assert.True(view.Bounds.Right < 0 && view.Bounds.Bottom < 0);
        Assert.False(view.IsHitTestVisible);
        Assert.False(view.Focusable);
    }

    [Fact]
    public async Task SwitchingTabs_KeepsAllTaskViewsAttachedToTheirHost()
    {
        var workspace = new EmbeddedBrowserWorkspaceViewModel();
        var taskId = Guid.NewGuid();
        var pane = workspace.Open(taskId);
        await using var session = Session(workspace, taskId, pane);
        var first = pane.Content;
        var secondId = await session.NewTabAsync(null, TestContext.Current.CancellationToken);
        var second = pane.Content;

        Assert.NotSame(first, second);
        Assert.Equal(2, workspace.Host.Children.Count);
        Assert.Same(workspace.Host, first!.Parent);
        Assert.Same(workspace.Host, second!.Parent);
        await session.CloseTabAsync(secondId, TestContext.Current.CancellationToken);
        Assert.Same(first, pane.Content);
        Assert.Same(first, Assert.Single(workspace.Host.Children));
        Assert.Null(second.Parent);
    }

    private static EmbeddedBrowserSession Session(EmbeddedBrowserWorkspaceViewModel workspace,
        Guid taskId, EmbeddedBrowserPaneViewModel pane) => new(View(), pane,
            _ => Task.FromResult<NativeWebView.Controls.NativeWebView>(View()),
            () => workspace.Close(taskId, pane));

    [Fact]
    public void BlockingPresentation_ParksViewsWithoutReleasingOrReparenting()
    {
        using var view = View();
        var host = new AppBrowserHost();
        var viewport = new AppBrowserViewport();
        var rectangle = new Rect(980, 20, 277, 640);
        host.Register(view);
        host.UpdateViewport(viewport, view, rectangle);
        Arrange(host);

        host.IsPresentationBlocked = true;
        Arrange(host);
        Assert.True(view.Bounds.Right < 0);
        Assert.Equal(rectangle.Size, view.Bounds.Size);
        Assert.Same(host, view.Parent);
        Assert.False(view.IsHitTestVisible);
        Assert.False(view.Focusable);
        host.IsPresentationBlocked = false;
        Arrange(host);
        Assert.Equal(rectangle, view.Bounds);
        Assert.True(view.IsHitTestVisible);
    }

    private static AppEmbeddedBrowserView View() => new(new WindowsNativeWebViewBackend());

    private static void Arrange(AppBrowserHost host)
    {
        var size = new Size(1280, 820);
        host.Measure(size);
        host.Arrange(new Rect(size));
    }
}
