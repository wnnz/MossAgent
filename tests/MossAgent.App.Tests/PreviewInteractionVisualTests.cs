using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PreviewInteractionVisualTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 300)]
    [InlineData(true, 300)]
    [InlineData(false, 640)]
    [InlineData(true, 640)]
    public Task SearchKeyboard_HighlightsAndScrollsWithoutOverflow(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        using var pane = new FilePreviewPaneViewModel(new InspectorProbeToolExecutor
        {
            Execute = (_, _, _) => Task.FromResult(new ToolResult(true, "ok", Artifacts: [new("file", "snapshot", "text/plain", 0, "hash")]))
        },
            new PreviewProbeArtifactReader
            {
                Read = (_, _, _) => Task.FromResult(System.Text.Encoding.UTF8.GetBytes(
                    "match\n" + string.Concat(Enumerable.Repeat("line\n", 60)) + "match"))
            });
        pane.Attach(new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.ReadOnly));
        await pane.OpenAsync("test.cs", TestContext.Current.CancellationToken);
        var preview = new AppFilePreview { DataContext = pane };
        var window = new AppWindow
        {
            Content = preview,
            Width = width,
            Height = 450,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var copy = preview.FindControl<AppIconButton>("CopyDefaultButton")!;
            copy.Focus();
            window.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, "f");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(pane.Active!.Search.IsOpen);
            var input = preview.FindControl<AppInput>("SearchInput")!;
            Assert.True(input.IsFocused);
            input.Text = "match";
            window.UpdateLayout();
            Assert.Equal(2, pane.Active.Search.MatchCount);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal(61, pane.Active.Search.SelectedLine);
            var code = Assert.Single(preview.GetVisualDescendants().OfType<AppCodePreview>());
            var current = Assert.Single(code.GetVisualDescendants().OfType<AppBorder>(), row => row.Classes.Contains("current-match"));
            Assert.NotNull(current.Background);
            Assert.Equal(2, code.GetVisualDescendants().OfType<AppBorder>().Count(row => row.Classes.Contains("search-match")));
            Assert.True(code.FindControl<AppScrollViewer>("Scroller")!.Offset.Y > 0);
            Assert.All(preview.GetVisualDescendants().OfType<AppIconButton>().Where(button => button.IsEffectivelyVisible), button =>
                Assert.True(button.TranslatePoint(new Point(button.Bounds.Width, 0), window)!.Value.X <= width));
            window.KeyPress(Key.Enter, RawInputModifiers.Shift, PhysicalKey.Enter, null);
            Assert.Equal(0, pane.Active.Search.SelectedLine);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Assert.False(pane.Active.Search.IsOpen);
            Assert.True(copy.IsFocused);
            Assert.DoesNotContain(code.GetVisualDescendants().OfType<AppBorder>(), row => row.Classes.Contains("search-match"));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ImageFitZoomAndResize_AreBoundedAndTaskOwned(bool dark) => _session.DispatchAsync(() =>
    {
        using var tab = new FilePreviewTabViewModel("image.png", "image.png", "image.png")
        { Image = new WriteableBitmap(new PixelSize(800, 600), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul), IsReady = true };
        var preview = new AppImagePreview { DataContext = tab, Source = tab.Image };
        preview.Bind(AppImagePreview.SourceProperty, new Avalonia.Data.Binding(nameof(tab.Image)));
        preview.Bind(AppImagePreview.IsFitProperty, new Avalonia.Data.Binding(nameof(tab.IsFit)));
        preview.Bind(AppImagePreview.ScaleProperty, new Avalonia.Data.Binding(nameof(tab.Scale)));
        var window = new AppWindow
        {
            Content = preview,
            Width = 300,
            Height = 250,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var surface = preview.FindControl<AppImage>("ImageSurface")!;
            var frame = preview.FindControl<AppBorder>("ImageFrame")!;
            Assert.Equal(new CornerRadius(8), frame.CornerRadius);
            Assert.True(frame.ClipToBounds);
            Assert.InRange(Math.Abs(surface.Bounds.Width - frame.Bounds.Width), 0, 1);
            Assert.InRange(Math.Abs(surface.Bounds.Height - frame.Bounds.Height), 0, 1);
            Assert.InRange(surface.Width, 1, 276);
            Assert.InRange(surface.Height, 1, 226);
            Assert.False(tab.Search.ShowCommand.CanExecute(null));
            tab.ZoomInCommand.Execute(null);
            window.UpdateLayout();
            Assert.False(tab.IsFit);
            Assert.Equal(1000, surface.Width);
            Assert.Equal(surface.Bounds.Size, frame.Bounds.Size);
            var scroller = preview.FindControl<AppScrollViewer>("Scroller")!;
            Assert.True(scroller.Extent.Width > scroller.Viewport.Width);
            scroller.Offset = new Vector(100, 50);
            Assert.True(scroller.Offset.X > 0);
            for (var index = 0; index < 40; index++) tab.ZoomInCommand.Execute(null);
            Assert.Equal(4, tab.Scale);
            Assert.False(tab.ZoomInCommand.CanExecute(null));
            for (var index = 0; index < 40; index++) tab.ZoomOutCommand.Execute(null);
            Assert.Equal(.1, tab.Scale);
            Assert.False(tab.ZoomOutCommand.CanExecute(null));
            tab.ActualSizeCommand.Execute(null);
            Assert.Equal(1, tab.Scale);
            tab.FitCommand.Execute(null);
            window.Width = 220;
            window.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(220, preview.Bounds.Width);
            Assert.InRange(surface.Width, 1, 196);
            tab.Image = null;
            window.UpdateLayout();
            Assert.Equal(0, surface.Width);
            Assert.False(tab.ZoomInCommand.CanExecute(null));
            tab.Dispose();
            Assert.False(tab.ActualSizeCommand.CanExecute(null));
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);
}
