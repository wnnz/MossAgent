using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TerminalVtLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(280, false)]
    [InlineData(470, false)]
    [InlineData(280, true)]
    [InlineData(470, true)]
    public Task SharedGridUsesMeasuredCellsAndDoesNotHideInitialPrompt(int width, bool dark) => _session.DispatchAsync(() =>
    {
        using var source = new TerminalVtScreen();
        source.Process("PS synthetic> \u001b[31m红色\u001b[0m\r\nnext");
        var frame = source.Capture(Guid.NewGuid(), Guid.NewGuid(), null, false);
        var viewport = new AppTerminalViewport { Frame = frame };
        var window = new AppWindow
        {
            Width = width,
            Height = 300,
            Content = viewport,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var screen = Assert.Single(viewport.GetVisualDescendants().OfType<AppTerminalScreen>());
            var scroll = Assert.Single(viewport.GetVisualDescendants().OfType<AppScrollViewer>());
            Assert.Equal(12, screen.FontSize);
            Assert.Equal(18, screen.CellHeight);
            Assert.Equal(36, screen.DesiredSize.Height);
            Assert.True(screen.Bounds.Width > width);
            Assert.Equal(0, scroll.Offset.Y);
            Assert.True(scroll.Extent.Width > scroll.Viewport.Width);
            Assert.False(scroll.AllowAutoHide);
            Assert.True(scroll.Viewport.Height < scroll.Bounds.Height);
            var bar = Assert.Single(viewport.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>(),
                item => item.Orientation == Avalonia.Layout.Orientation.Horizontal);
            Assert.Equal(12, bar.Bounds.Height);
            Assert.True(Avalonia.Application.Current!.Resources.TryGetResource("TextPrimaryBrush", window.ActualThemeVariant, out var foreground));
            Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(foreground).Color, Assert.IsAssignableFrom<ISolidColorBrush>(screen.Foreground).Color);
            Assert.Empty(viewport.GetVisualDescendants().OfType<AppSelectableText>());
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ScrollbackRestoresByScreenAndAlternateModeWithoutStealingUserScroll() => _session.DispatchAsync(() =>
    {
        using var source = new TerminalVtScreen();
        source.Process(string.Concat(Enumerable.Range(0, 90).Select(index => $"line-{index}\r\n")));
        var first = source.Capture(Guid.NewGuid(), Guid.NewGuid(), null, false);
        var second = first with { ViewId = Guid.NewGuid(), TaskId = Guid.NewGuid() };
        var viewport = new AppTerminalViewport { Frame = first };
        var window = new AppWindow { Width = 470, Height = 300, Content = viewport };
        window.Show();
        try
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var scroll = Assert.Single(viewport.GetVisualDescendants().OfType<AppScrollViewer>());
            Assert.True(scroll.Offset.Y > 0);
            scroll.Offset = new Vector(40, 36);
            Dispatcher.UIThread.RunJobs();
            viewport.Frame = first with { Revision = 2 };
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new Vector(40, 36), scroll.Offset);
            viewport.Frame = second;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.True(scroll.Offset.Y > 36);
            viewport.Frame = first;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new Vector(40, 36), scroll.Offset);
            viewport.Frame = first with { AlternateScreen = true, DisplayRows = 30 };
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, scroll.Offset.Y);
            viewport.Frame = first;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new Vector(40, 36), scroll.Offset);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task GlyphShapingCacheIsBoundedAndClearedOnThemeChange() => _session.DispatchAsync(() =>
    {
        var cache = new TerminalGlyphCache();
        var font = new FontFamily("Consolas");
        cache.Reset(font, 12, Colors.Black);
        for (var index = 0; index < 1000; index++)
        {
            cache.Get(new(char.ConvertFromUtf32(0x400 + index), 1, 0, 0, true, true, 0, false, false, false), Colors.Black);
            Assert.InRange(cache.Count, 1, TerminalGlyphCache.MaximumEntries);
        }
        cache.Reset(font, 12, Colors.White);
        Assert.Equal(0, cache.Count);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);
}
