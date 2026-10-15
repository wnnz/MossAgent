using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SidebarScrollBarComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RestingThumb_HasReferenceWidthAndNoInheritedPanningScale(bool dark) => WithViewport(dark, 40,
        (window, scroll, content) =>
        {
            var bar = VerticalBar(scroll);
            var thumb = Assert.Single(bar.GetVisualDescendants().OfType<Thumb>());
            var surface = Assert.Single(thumb.GetVisualDescendants().OfType<AppBorder>());
            Assert.True(bar.IsVisible);
            Assert.Equal(6, surface.Bounds.Width);
            Assert.Equal(new CornerRadius(3), surface.CornerRadius);
            Assert.Equal(Matrix.Identity, thumb.RenderTransform?.Value ?? Matrix.Identity);
            var right = surface.TranslatePoint(new Point(surface.Bounds.Width, 0), window)!.Value.X;
            Assert.Equal(window.ClientSize.Width - 3, right, precision: 3);
            Assert.Equal(247, content.Bounds.Width);
            var color = Assert.IsAssignableFrom<ISolidColorBrush>(surface.Background).Color;
            Assert.Equal(Color.Parse(dark ? "#40474D" : "#E7E9EB"), color);
            var lines = bar.GetVisualDescendants().OfType<RepeatButton>()
                .Where(button => button.Name is "PART_LineUpButton" or "PART_LineDownButton").ToArray();
            Assert.Equal(2, lines.Length);
            Assert.All(lines, button => Assert.False(button.IsVisible));
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task HoverAndPressedThumb_KeepFullWidthWithoutTrackOrArrowChrome(bool dark) => WithViewport(dark, 40,
        (window, scroll, content) =>
        {
            var bar = VerticalBar(scroll);
            var thumb = Assert.Single(bar.GetVisualDescendants().OfType<Thumb>());
            var surface = Assert.Single(thumb.GetVisualDescendants().OfType<AppBorder>());
            var start = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            window.MouseMove(start);
            window.UpdateLayout();
            AssertThumbChrome(bar, thumb, surface, dark);
            window.MouseDown(start, MouseButton.Left);
            window.UpdateLayout();
            AssertThumbChrome(bar, thumb, surface, dark);
            window.MouseUp(start, MouseButton.Left);
            window.MouseMove(new Point(100, 20));
            window.UpdateLayout();
            var color = Assert.IsAssignableFrom<ISolidColorBrush>(surface.Background).Color;
            Assert.Equal(Color.Parse(dark ? "#40474D" : "#E7E9EB"), color);
            Assert.Equal(0, scroll.Offset.Y);
            Assert.Equal(247, content.Bounds.Width);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PointerDragAndPageClick_ScrollWithoutChangingTheContentWidth(bool dark) => WithViewport(dark, 40,
        (window, scroll, content) =>
        {
            var bar = VerticalBar(scroll);
            var thumb = Assert.Single(bar.GetVisualDescendants().OfType<Thumb>());
            var start = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(0, 30));
            window.MouseUp(start + new Vector(0, 30), MouseButton.Left);
            window.UpdateLayout();
            Assert.True(scroll.Offset.Y > 0);
            Assert.Equal(247, content.Bounds.Width);
            Assert.Equal(Matrix.Identity, thumb.RenderTransform?.Value ?? Matrix.Identity);
            var afterDrag = scroll.Offset.Y;
            var page = bar.TranslatePoint(new Point(bar.Bounds.Width / 2, bar.Bounds.Height - 8), window)!.Value;
            window.MouseDown(page, MouseButton.Left);
            window.MouseUp(page, MouseButton.Left);
            window.UpdateLayout();
            Assert.True(scroll.Offset.Y > afterDrag);
            var afterPage = scroll.Offset.Y;
            window.MouseWheel(new Point(100, 130), new Vector(0, 2));
            window.UpdateLayout();
            Assert.True(scroll.Offset.Y < afterPage);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DisabledViewport_RejectsThumbDragging(bool dark) => WithViewport(dark, 40,
        (window, scroll, content) =>
        {
            var thumb = Assert.Single(VerticalBar(scroll).GetVisualDescendants().OfType<Thumb>());
            var start = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            scroll.IsEnabled = false;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(0, 50));
            window.MouseUp(start + new Vector(0, 50), MouseButton.Left);
            Assert.Equal(0, scroll.Offset.Y);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FittingContent_HidesTheBarAndDoesNotReserveItsWidth(bool dark) => WithViewport(dark, 2,
        (window, scroll, content) =>
        {
            Assert.False(VerticalBar(scroll).IsVisible);
            Assert.Equal(247, content.Bounds.Width);
            window.MouseWheel(new Point(100, 130), new Vector(0, -3));
            window.UpdateLayout();
            Assert.Equal(0, scroll.Offset.Y);
        });

    private Task WithViewport(bool dark, int count, Action<AppWindow, AppScrollViewer, StackPanel> action) => _session.Dispatch(() =>
    {
        var content = new StackPanel();
        foreach (var index in Enumerable.Range(0, count))
            content.Children.Add(new AppTaskNavigationRow
            {
                Title = "Sidebar task " + index,
                CommandParameter = index,
                SelectCommand = new RelayCommand(() => { })
            });
        var scroll = new AppScrollViewer { Classes = { "sidebar-navigation-scroll" }, Content = content };
        Grid.SetRow(scroll, 1);
        var window = new AppWindow
        {
            Width = 264,
            Height = 280,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new AppBorder
            {
                Classes = { "workspace-sidebar-surface" },
                Padding = new Thickness(8, 8, 8, 12),
                Child = new Grid
                {
                    RowDefinitions = new RowDefinitions("32,*"),
                    Children = { new AppButton { Content = "Outside the viewport" }, scroll }
                }
            }
        };
        window.Show();
        try { window.UpdateLayout(); action(window, scroll, content); }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static ScrollBar VerticalBar(AppScrollViewer scroll) =>
        Assert.Single(scroll.GetVisualDescendants().OfType<ScrollBar>(), bar => bar.Name == "PART_VerticalScrollBar");

    private static void AssertThumbChrome(ScrollBar bar, Thumb thumb, AppBorder surface, bool dark)
    {
        Assert.Equal(6, surface.Bounds.Width);
        Assert.Equal(Matrix.Identity, thumb.RenderTransform?.Value ?? Matrix.Identity);
        var color = Assert.IsAssignableFrom<ISolidColorBrush>(surface.Background).Color;
        Assert.Equal(Color.Parse(dark ? "#6D757C" : "#CED2D6"), color);
        var root = Assert.Single(bar.GetVisualDescendants().OfType<Grid>(), item => item.Name == "Root");
        Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(root.Background).Color);
        var track = Assert.Single(bar.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>(),
            item => item.Name == "TrackRect");
        Assert.Equal(0, track.Opacity);
        Assert.All(bar.GetVisualDescendants().OfType<RepeatButton>()
            .Where(button => button.Name is "PART_LineUpButton" or "PART_LineDownButton"),
            button => Assert.False(button.IsVisible));
    }
}
