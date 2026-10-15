using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PreviewScrollBarInteractionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 1100)]
    [InlineData(true, 1100)]
    [InlineData(false, 420)]
    [InlineData(true, 420)]
    public Task IdleArrowsRemainVisibleWithOnlyRealOverflow(bool dark, double width) => _session.Dispatch(() =>
    {
        var preview = new AppDiffPreview();
        var window = Show(preview, dark, width);
        try
        {
            var scrolls = preview.GetVisualDescendants().OfType<AppScrollViewer>().ToArray();
            Assert.Equal(2, scrolls.Length);
            foreach (var scroll in scrolls)
            {
                var bar = Bar(scroll);
                Assert.False(scroll.AllowAutoHide);
                Assert.False(bar.AllowAutoHide);
                Assert.Equal(16, bar.Bounds.Height);
                Assert.Equal(1, bar.Opacity);
                Assert.Equal(width < 500, scroll.Extent.Width > scroll.Viewport.Width);
                Assert.Equal(width < 500, bar.IsEffectivelyEnabled);
                Assert.Equal(width < 500, Assert.Single(bar.GetVisualDescendants().OfType<Thumb>()).IsVisible);
                foreach (var button in LineButtons(bar))
                {
                    Assert.Equal(1, button.Opacity);
                    Assert.True(button.IsVisible);
                    Assert.Equal(new Size(16, 16), button.Bounds.Size);
                    var glyph = Assert.Single(button.GetVisualDescendants().OfType<AppIcon>());
                    Assert.Equal(8, glyph.Size);
                    Assert.NotNull(glyph.Fill);
                    Assert.Null(glyph.Stroke);
                }
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task NativeArrowClicksRespectColumnOwnershipAndDisabledState(bool dark, bool disabled) => _session.Dispatch(() =>
    {
        var preview = new AppDiffPreview { IsEnabled = !disabled };
        var window = Show(preview, dark, 420);
        try
        {
            var scrolls = preview.GetVisualDescendants().OfType<AppScrollViewer>().ToArray();
            var buttons = LineButtons(Bar(scrolls[0]));
            Click(window, buttons.Single(button => button.Name == "PART_LineDownButton"));
            Assert.Equal(!disabled, scrolls[0].Offset.X > 0);
            Assert.Equal(default, scrolls[1].Offset);
            Click(window, buttons.Single(button => button.Name == "PART_LineUpButton"));
            Assert.Equal(default, scrolls[0].Offset);
            Assert.Equal(default, scrolls[1].Offset);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NativeThumbDragKeepsTheOtherColumnUnchanged(bool dark) => _session.Dispatch(() =>
    {
        var preview = new AppDiffPreview();
        var window = Show(preview, dark, 420);
        try
        {
            var scrolls = preview.GetVisualDescendants().OfType<AppScrollViewer>().ToArray();
            var thumb = Assert.Single(Bar(scrolls[1]).GetVisualDescendants().OfType<Thumb>());
            var start = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(12, 0));
            window.MouseUp(start + new Vector(12, 0), MouseButton.Left);
            window.UpdateLayout();
            Assert.True(scrolls[1].Offset.X > 0);
            Assert.Equal(default, scrolls[0].Offset);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppWindow Show(AppDiffPreview preview, bool dark, double width)
    {
        var window = new AppWindow
        {
            Width = width,
            Height = 200,
            Content = preview,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static ScrollBar Bar(AppScrollViewer scroll) => Assert.Single(scroll.GetVisualDescendants().OfType<ScrollBar>(),
        bar => bar.Name == "PART_HorizontalScrollBar");

    private static RepeatButton[] LineButtons(ScrollBar bar) => bar.GetVisualDescendants().OfType<RepeatButton>()
        .Where(button => button.Name is "PART_LineUpButton" or "PART_LineDownButton").ToArray();

    private static void Click(AppWindow window, RepeatButton button)
    {
        var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.UpdateLayout();
    }
}
