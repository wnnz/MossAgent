using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using MossAgent.App.Controls;
using SkiaSharp;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AppIconDrawingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(IconDrawingTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ExplorerDrawingRendersYellowAndBlueWithoutChangingMonochromeIcons(bool dark) => _session.DispatchAsync(() =>
    {
        var icon = new AppIcon { Size = 24 };
        var window = new AppWindow { Content = icon, Width = 24, Height = 24, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            icon.Drawing = Assert.IsType<DrawingGroup>(window.FindResource("FilePreviewExplorerDrawing"));
            using var pixels = Render(icon, 24);
            Assert.Equal(new SKColor(255, 211, 72), pixels.GetPixel(5, 15));
            Assert.Equal(new SKColor(30, 154, 203), pixels.GetPixel(8, 18));
            Assert.Equal(new SKColor(229, 160, 0), pixels.GetPixel(5, 7));
            Assert.Equal(new SKColor(246, 247, 247), pixels.GetPixel(12, 1));
            Assert.Null(new AppIcon().Drawing);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(12)]
    [InlineData(14)]
    [InlineData(24)]
    public Task DrawingIsUniformlyScaledCenteredAndTakesPrecedenceOverGeometry(int size) => _session.DispatchAsync(() =>
    {
        var drawing = new GeometryDrawing { Brush = Brushes.Red, Geometry = new RectangleGeometry(new Rect(-10, 20, 20, 10)) };
        var icon = new AppIcon { Size = size, Drawing = drawing, Data = new RectangleGeometry(new Rect(0, 0, 10, 10)), Fill = Brushes.Blue };
        icon.Measure(new Size(size, size));
        icon.Arrange(new Rect(0, 0, size, size));
        using var pixels = Render(icon, size);
        Assert.Equal(new SKColor(255, 0, 0), pixels.GetPixel(size / 2, size / 2));
        Assert.Equal((byte)0, pixels.GetPixel(size / 2, 0).Alpha);
        Assert.Equal((byte)0, pixels.GetPixel(size / 2, size - 1).Alpha);
        Assert.Equal(size, icon.Bounds.Width);
        Assert.Equal(size, icon.Bounds.Height);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ClearingDrawingRestoresGeometryAndEmptyDrawingSuppressesIt() => _session.DispatchAsync(() =>
    {
        var icon = new AppIcon { Size = 14, Data = new RectangleGeometry(new Rect(0, 0, 10, 10)), Fill = Brushes.Blue };
        icon.Measure(new Size(14, 14));
        icon.Arrange(new Rect(0, 0, 14, 14));
        icon.Drawing = new GeometryDrawing { Brush = Brushes.Red, Geometry = new RectangleGeometry(new Rect(0, 0, 10, 10)) };
        using (var pixels = Render(icon, 14)) Assert.Equal(new SKColor(255, 0, 0), pixels.GetPixel(7, 7));
        icon.Drawing = new DrawingGroup();
        using (var pixels = Render(icon, 14)) Assert.Equal((byte)0, pixels.GetPixel(7, 7).Alpha);
        icon.Drawing = null;
        using (var pixels = Render(icon, 14)) Assert.Equal(new SKColor(0, 0, 255), pixels.GetPixel(7, 7));
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    private static SKBitmap Render(AppIcon icon, int size)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(size, size));
        bitmap.Render(icon);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return SKBitmap.Decode(stream.ToArray());
    }
}
