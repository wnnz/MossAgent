using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ContentScrollBarComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task RestingChrome_UsesASixDipRoundedThumbAndTransparentTrack(bool dark, bool horizontal) => WithViewport(dark, horizontal, false,
        (window, scroll) =>
        {
            var bar = Bar(scroll, horizontal);
            var thumb = Assert.Single(bar.GetVisualDescendants().OfType<Thumb>());
            var surface = Assert.Single(thumb.GetVisualDescendants().OfType<AppBorder>());
            Assert.True(bar.IsVisible);
            Assert.True(scroll.AllowAutoHide);
            Assert.Equal(12, horizontal ? bar.Bounds.Height : bar.Bounds.Width);
            Assert.Equal(6, horizontal ? surface.Bounds.Height : surface.Bounds.Width);
            Assert.Equal(new CornerRadius(3), surface.CornerRadius);
            Assert.Equal(Matrix.Identity, thumb.RenderTransform?.Value ?? Matrix.Identity);
            Assert.Null(thumb.Transitions);
            Assert.Equal(Color.Parse(dark ? "#40474D" : "#E4E4E5"), BrushColor(surface.Background));
            Assert.Equal(Colors.Transparent, BrushColor(bar.Background));
            var track = Assert.Single(bar.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>(), rectangle => rectangle.Name == "TrackRect");
            Assert.Equal(0, track.Opacity);
            Assert.All(bar.GetVisualDescendants().OfType<RepeatButton>()
                .Where(button => button.Name is "PART_LineUpButton" or "PART_LineDownButton"), button => Assert.False(button.IsVisible));
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task HoverDragPageAndWheel_PreserveGeometryAndNativeScrolling(bool dark, bool horizontal) => WithViewport(dark, horizontal, false,
        (window, scroll) =>
        {
            var bar = Bar(scroll, horizontal);
            var thumb = Assert.Single(bar.GetVisualDescendants().OfType<Thumb>());
            var surface = Assert.Single(thumb.GetVisualDescendants().OfType<AppBorder>());
            var viewport = scroll.Viewport;
            var start = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            var movement = horizontal ? new Vector(30, 0) : new Vector(0, 30);
            window.MouseMove(start);
            Flush(window);
            Assert.Equal(Color.Parse(dark ? "#6D757C" : "#CED2D6"), BrushColor(surface.Background));
            window.MouseDown(start, MouseButton.Left);
            Flush(window);
            Assert.Equal(Color.Parse(dark ? "#6D757C" : "#CED2D6"), BrushColor(surface.Background));
            window.MouseMove(start + movement);
            window.MouseUp(start + movement, MouseButton.Left);
            Flush(window);
            var afterDrag = Offset(scroll, horizontal);
            Assert.True(afterDrag > 0);
            var page = bar.TranslatePoint(new Point(bar.Bounds.Width - 8, bar.Bounds.Height - 8), window)!.Value;
            window.MouseDown(page, MouseButton.Left);
            window.MouseUp(page, MouseButton.Left);
            Flush(window);
            var afterPage = Offset(scroll, horizontal);
            Assert.True(afterPage > afterDrag);
            window.MouseWheel(new Point(100, 80), horizontal ? new Vector(2, 0) : new Vector(0, 2));
            Flush(window);
            Assert.True(Offset(scroll, horizontal) < afterPage);
            Assert.Equal(viewport, scroll.Viewport);
            Assert.Equal(6, horizontal ? surface.Bounds.Height : surface.Bounds.Width);
            Assert.Equal(Matrix.Identity, thumb.RenderTransform?.Value ?? Matrix.Identity);
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task DisabledViewport_RejectsDraggingAndWheelInput(bool dark, bool horizontal) => WithViewport(dark, horizontal, false,
        (window, scroll) =>
        {
            var thumb = Assert.Single(Bar(scroll, horizontal).GetVisualDescendants().OfType<Thumb>());
            var start = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            scroll.IsEnabled = false;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + (horizontal ? new Vector(50, 0) : new Vector(0, 50)));
            window.MouseUp(start, MouseButton.Left);
            window.MouseWheel(new Point(100, 80), horizontal ? new Vector(-2, 0) : new Vector(0, -2));
            Flush(window);
            Assert.Equal(default, scroll.Offset);
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task FittingContent_HidesTheBarWithoutInventingAnOffset(bool dark, bool horizontal) => WithViewport(dark, horizontal, true,
        (window, scroll) =>
        {
            Assert.False(Bar(scroll, horizontal).IsVisible);
            window.MouseWheel(new Point(100, 80), horizontal ? new Vector(-3, 0) : new Vector(0, -3));
            Flush(window);
            Assert.Equal(default, scroll.Offset);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ThemeChanges_RecolorTheExistingThumbWithoutChangingItsGeometry(bool horizontal) => WithViewport(false, horizontal, false,
        (window, scroll) =>
        {
            var thumb = Assert.Single(Bar(scroll, horizontal).GetVisualDescendants().OfType<Thumb>());
            var surface = Assert.Single(thumb.GetVisualDescendants().OfType<AppBorder>());
            var viewport = scroll.Viewport;
            foreach (var dark in new[] { true, false })
            {
                window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
                Flush(window);
                Assert.Same(surface, Assert.Single(thumb.GetVisualDescendants().OfType<AppBorder>()));
                Assert.Equal(Color.Parse(dark ? "#40474D" : "#E4E4E5"), BrushColor(surface.Background));
                Assert.Equal(6, horizontal ? surface.Bounds.Height : surface.Bounds.Width);
                Assert.Equal(viewport, scroll.Viewport);
            }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ThinListBox_UsesTheSharedChromeAndItsNavigationPalette(bool dark) => _session.Dispatch(() =>
    {
        var list = new AppListBox { Classes = { "thin-scrollbar" } };
        for (var index = 0; index < 30; index++) list.Items.Add(new AppText { Text = "Row " + index });
        var window = new AppWindow { Width = 320, Height = 220, Content = list, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            Flush(window);
            var bar = Assert.Single(list.GetVisualDescendants().OfType<ScrollBar>(), candidate => candidate.Name == "PART_VerticalScrollBar");
            var thumb = Assert.Single(bar.GetVisualDescendants().OfType<Thumb>());
            var surface = Assert.Single(thumb.GetVisualDescendants().OfType<AppBorder>());
            Assert.True(bar.IsVisible);
            Assert.Equal(6, surface.Bounds.Width);
            Assert.Equal(Color.Parse(dark ? "#40474D" : "#E7E9EB"), BrushColor(surface.Background));
            window.MouseWheel(new Point(100, 80), new Vector(0, -2));
            Flush(window);
            Assert.True(bar.Value > 0);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private Task WithViewport(bool dark, bool horizontal, bool fits, Action<AppWindow, AppScrollViewer> action) => _session.Dispatch(() =>
    {
        var scroll = new AppScrollViewer
        {
            HorizontalScrollBarVisibility = horizontal ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = horizontal ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto,
            Content = new AppBorder { Width = horizontal ? fits ? 200 : 1800 : double.NaN, Height = horizontal || fits ? 100 : 1800 }
        };
        var window = new AppWindow
        {
            Width = 320,
            Height = 220,
            Content = scroll,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try { Flush(window); action(window, scroll); }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static ScrollBar Bar(AppScrollViewer scroll, bool horizontal) => Assert.Single(scroll.GetVisualDescendants()
        .OfType<ScrollBar>(), bar => bar.Name == (horizontal ? "PART_HorizontalScrollBar" : "PART_VerticalScrollBar"));

    private static double Offset(AppScrollViewer scroll, bool horizontal) => horizontal ? scroll.Offset.X : scroll.Offset.Y;

    private static Color BrushColor(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static void Flush(AppWindow window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
