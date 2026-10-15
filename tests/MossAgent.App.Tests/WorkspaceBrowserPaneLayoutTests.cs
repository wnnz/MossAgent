using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceBrowserPaneLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Viewport_FollowsInspectorDragResizeAndReopen(bool dark) => _session.Dispatch(() =>
    {
        var host = new AppBrowserHost();
        var browser = new AppBorder();
        var background = new AppBorder();
        host.Register(browser);
        host.Register(background);
        var viewport = new AppBrowserViewport { Host = host, View = browser };
        var panes = new AppWorkspacePanes
        {
            PrimaryPane = new AppBorder(),
            InspectorPane = new AppBorder { Child = viewport },
            IsInspectorOpen = true
        };
        viewport.Bind(Visual.IsVisibleProperty, new Binding(nameof(panes.IsInspectorOpen)) { Source = panes });
        var window = Window(panes, host, dark);
        window.Show();
        try
        {
            window.UpdateLayout();
            AssertPlacement(viewport, host, browser, background);
            var splitter = Assert.Single(panes.GetVisualDescendants().OfType<AppPaneSplitter>());
            var position = splitter.TranslatePoint(default, window)!.Value;
            var before = browser.Bounds.Width;
            var start = new Point(position.X + 2, 400);
            var finish = start.WithX(start.X - 120);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(finish);
            window.MouseUp(finish, MouseButton.Left);
            window.UpdateLayout();
            var preferred = browser.Bounds.Width;
            Assert.InRange(preferred - before, 119, 121);
            AssertPlacement(viewport, host, browser, background);
            Resize(window, 660);
            Assert.InRange(browser.Bounds.Width, 280, 300);
            AssertPlacement(viewport, host, browser, background);
            Resize(window, 1600);
            Assert.InRange(browser.Bounds.Width - preferred, -1, 1);
            panes.IsInspectorOpen = false;
            window.UpdateLayout();
            Assert.True(browser.Bounds.Right < 0);
            Assert.False(browser.IsHitTestVisible);
            Assert.Same(host, browser.Parent);
            panes.IsInspectorOpen = true;
            window.UpdateLayout();
            Assert.InRange(browser.Bounds.Width - preferred, -1, 1);
            AssertPlacement(viewport, host, browser, background);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task BlockedBrowser_UsesLatestViewportWhenPresentationResumes(bool dark) => _session.Dispatch(() =>
    {
        var host = new AppBrowserHost();
        var browser = new AppBorder();
        var background = new AppBorder();
        host.Register(browser);
        host.Register(background);
        var viewport = new AppBrowserViewport { Host = host, View = browser };
        var panes = new AppWorkspacePanes
        {
            PrimaryPane = new AppBorder(),
            InspectorPane = new AppBorder { Child = viewport },
            IsInspectorOpen = true
        };
        var window = Window(panes, host, dark);
        window.Show();
        try
        {
            window.UpdateLayout();
            host.IsPresentationBlocked = true;
            Resize(window, 980);
            Assert.True(browser.Bounds.Right < 0);
            Assert.False(browser.IsHitTestVisible);
            Assert.False(browser.Focusable);
            Assert.Same(host, browser.Parent);
            host.IsPresentationBlocked = false;
            window.UpdateLayout();
            AssertPlacement(viewport, host, browser, background);
            Assert.InRange(browser.Bounds.Width, 460, 462);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AncestorVisibility_ParksBrowserWithoutChangingViewportVisibility() => _session.Dispatch(() =>
    {
        var host = new AppBrowserHost();
        var browser = new AppBorder();
        var background = new AppBorder();
        host.Register(browser);
        host.Register(background);
        var viewport = new AppBrowserViewport { Host = host, View = browser };
        var inspector = new AppBorder { Child = viewport };
        var panes = new AppWorkspacePanes
        {
            PrimaryPane = new AppBorder(),
            InspectorPane = inspector,
            IsInspectorOpen = true
        };
        var window = Window(panes, host, false);
        window.Show();
        try
        {
            window.UpdateLayout();
            inspector.IsVisible = false;
            window.UpdateLayout();
            Assert.True(viewport.IsVisible);
            Assert.True(browser.Bounds.Right < 0);
            Assert.False(browser.IsHitTestVisible);
            inspector.IsVisible = true;
            window.UpdateLayout();
            AssertPlacement(viewport, host, browser, background);
            inspector.Child = null;
            window.UpdateLayout();
            Assert.True(browser.Bounds.Right < 0);
            inspector.Child = viewport;
            window.UpdateLayout();
            AssertPlacement(viewport, host, browser, background);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppWindow Window(AppWorkspacePanes panes, AppBrowserHost host, bool dark) => new()
    {
        Width = 1600,
        Height = 820,
        RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        Content = new Grid { Children = { panes, host } }
    };

    private static void AssertPlacement(AppBrowserViewport viewport, AppBrowserHost host,
        Control browser, Control background)
    {
        var expected = new Rect(viewport.TranslatePoint(default, host)!.Value, viewport.Bounds.Size);
        Assert.Equal(expected, browser.Bounds);
        Assert.True(browser.IsHitTestVisible);
        Assert.True(browser.Focusable);
        Assert.Same(host, browser.Parent);
        Assert.Same(host, background.Parent);
        Assert.True(background.Bounds.Right < 0);
        Assert.False(background.IsHitTestVisible);
        Assert.False(background.Focusable);
        Assert.Equal(2, host.Children.Count);
    }

    private static void Resize(AppWindow window, double width)
    {
        window.Width = width;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Assert.Equal(width, window.ClientSize.Width);
    }
}
