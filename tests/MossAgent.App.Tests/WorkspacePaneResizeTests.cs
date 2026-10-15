using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspacePaneResizeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PointerDrag_ResizesRealColumnsWithinSharedBounds(bool dark) => _session.Dispatch(() =>
    {
        var primary = new AppBorder();
        var inspector = new AppBorder();
        var panes = new AppWorkspacePanes { PrimaryPane = primary, InspectorPane = inspector, IsInspectorOpen = true };
        var window = new AppWindow
        {
            Width = 1600,
            Height = 820,
            Content = panes,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var splitter = Assert.Single(panes.GetVisualDescendants().OfType<AppPaneSplitter>());
            Assert.Equal(4, splitter.Bounds.Width);
            var position = splitter.TranslatePoint(default, window)!.Value;
            var width = inspector.Bounds.Width;
            var start = new Point(position.X + 2, 400);
            var finish = start.WithX(start.X - 120);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(finish);
            window.MouseUp(finish, MouseButton.Left);
            window.UpdateLayout();
            Assert.InRange(inspector.Bounds.Width - width, 119, 121);
            Assert.True(primary.Bounds.Width >= panes.MinimumPrimaryWidth);
            Assert.True(inspector.Bounds.Width >= panes.MinimumInspectorWidth);
            Assert.Same(primary, panes.PrimaryPane);
            Assert.Same(inspector, panes.InspectorPane);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task KeyboardResize_PreservesPreferenceThroughClampingAndReopen(bool dark) => _session.Dispatch(() =>
    {
        var primary = new AppBorder();
        var inspector = new AppBorder();
        var panes = new AppWorkspacePanes { PrimaryPane = primary, InspectorPane = inspector, IsInspectorOpen = true };
        var window = new AppWindow
        {
            Width = 1600,
            Height = 820,
            Content = panes,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var splitter = Assert.Single(panes.GetVisualDescendants().OfType<AppPaneSplitter>());
            Assert.True(splitter.Focus());
            var before = inspector.Bounds.Width;
            PressArrow(window, Key.Left);
            window.UpdateLayout();
            var preferred = inspector.Bounds.Width;
            Assert.InRange(preferred - before, 9, 11);
            Resize(window, 660);
            Assert.Equal(660, window.ClientSize.Width);
            Assert.InRange(inspector.Bounds.Width, 280, 300);
            Assert.True(primary.Bounds.Width >= panes.MinimumPrimaryWidth);
            Resize(window, 1600);
            Assert.InRange(inspector.Bounds.Width - preferred, -1, 1);
            panes.IsInspectorOpen = false;
            window.UpdateLayout();
            Assert.False(splitter.IsEffectivelyVisible);
            Assert.False(splitter.IsEffectivelyEnabled);
            Assert.Equal(300, inspector.Bounds.Width);
            Assert.Equal(1600, primary.Bounds.Width);
            var floating = inspector.TranslatePoint(default, panes)!.Value;
            Assert.Equal(58, floating.Y);
            Assert.Equal(8, panes.Bounds.Width - floating.X - inspector.Bounds.Width);
            panes.IsInspectorOpen = true;
            window.UpdateLayout();
            Assert.InRange(inspector.Bounds.Width - preferred, -1, 1);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task KeyboardResize_CannotCollapseEitherPane() => _session.Dispatch(() =>
    {
        var primary = new AppBorder();
        var inspector = new AppBorder();
        var panes = new AppWorkspacePanes { PrimaryPane = primary, InspectorPane = inspector, IsInspectorOpen = true };
        var window = new AppWindow { Width = 1000, Height = 600, Content = panes };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.True(Assert.Single(panes.GetVisualDescendants().OfType<AppPaneSplitter>()).Focus());
            foreach (var key in new[] { Key.Left, Key.Right })
            {
                for (var step = 0; step < 100; step++)
                {
                    PressArrow(window, key);
                    window.UpdateLayout();
                }
                Assert.True(primary.Bounds.Width >= panes.MinimumPrimaryWidth);
                Assert.True(inspector.Bounds.Width >= panes.MinimumInspectorWidth);
                Assert.InRange(primary.Bounds.Width + inspector.Bounds.Width, 995, 997);
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DetachAndReattach_PreservesPreferenceWithoutTrackingDetachedColumns() => _session.Dispatch(() =>
    {
        var primary = new AppBorder();
        var inspector = new AppBorder();
        var panes = new AppWorkspacePanes { PrimaryPane = primary, InspectorPane = inspector, IsInspectorOpen = true };
        var window = new AppWindow { Width = 1600, Height = 820, Content = panes };
        window.Show();
        try
        {
            window.UpdateLayout();
            var splitter = Assert.Single(panes.GetVisualDescendants().OfType<AppPaneSplitter>());
            Assert.True(splitter.Focus());
            PressArrow(window, Key.Left);
            window.UpdateLayout();
            var preferred = inspector.Bounds.Width;
            var layout = Assert.IsType<Grid>(panes.Content);
            window.Content = null;
            layout.ColumnDefinitions[2].Width = new GridLength(300);
            window.Content = panes;
            window.UpdateLayout();
            Assert.InRange(inspector.Bounds.Width - preferred, -1, 1);
            Assert.Same(primary, panes.PrimaryPane);
            Assert.Same(inspector, panes.InspectorPane);
            Assert.True(splitter.Focus());
            PressArrow(window, Key.Right);
            window.UpdateLayout();
            Assert.InRange(preferred - inspector.Bounds.Width, 9, 11);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void PressArrow(AppWindow window, Key key)
    {
        var physical = key == Key.Left ? PhysicalKey.ArrowLeft : PhysicalKey.ArrowRight;
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
    }

    private static void Resize(AppWindow window, double width)
    {
        window.Width = width;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
