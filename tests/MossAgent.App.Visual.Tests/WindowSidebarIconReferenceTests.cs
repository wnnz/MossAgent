using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using SkiaSharp;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class WindowSidebarIconReferenceTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 980, 640, 1)]
    [InlineData(false, 980, 640, 1.25)]
    [InlineData(false, 980, 640, 2)]
    [InlineData(false, 1280, 820, 1)]
    [InlineData(false, 1280, 820, 1.25)]
    [InlineData(false, 1280, 820, 2)]
    [InlineData(true, 980, 640, 1)]
    [InlineData(true, 980, 640, 1.25)]
    [InlineData(true, 980, 640, 2)]
    [InlineData(true, 1280, 820, 1)]
    [InlineData(true, 1280, 820, 1.25)]
    [InlineData(true, 1280, 820, 2)]
    public Task SidebarGlyph_RendersSquareCenteredInkWithoutChangingHitArea(bool dark, double width, double height, double scale) =>
        _session.Dispatch(() =>
        {
            var window = new MainWindow
            {
                Width = width,
                Height = height,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.MouseMove(new Point(width / 2, 100));
                window.UpdateLayout();
                var button = Assert.Single(window.GetVisualDescendants().OfType<AppIconButton>(),
                    candidate => candidate.Classes.Contains("titlebar-panel"));
                Assert.Equal(new Size(32, 30), button.Bounds.Size);
                AssertInkBounds(button, scale);
                var icon = Assert.IsType<AppIcon>(button.Content);
                Assert.Equal(10, icon.Size);
                Assert.Equal(1, icon.StrokeThickness);
                Assert.Equal(icon.Data!.Bounds.Width, icon.Data.Bounds.Height);
                var center = icon.TranslatePoint(new Point(icon.Bounds.Width / 2, icon.Bounds.Height / 2), button);
                Assert.NotNull(center);
                Assert.Equal(new Point(16, 15), center.Value);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static void AssertInkBounds(AppIconButton button, double scale)
    {
        using var bitmap = new RenderTargetBitmap(
            new PixelSize((int)(32 * scale), (int)(30 * scale)), new Vector(96 * scale, 96 * scale));
        bitmap.Render(button);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        using var pixels = SKBitmap.Decode(stream.ToArray());
        var bounds = InkBounds(pixels);
        Assert.InRange(Math.Abs(bounds.Width - bounds.Height), 0, 1);
        Assert.InRange(bounds.Width, 10 * scale, 12 * scale + 1);
        Assert.InRange(Math.Abs(bounds.Center.X - 16 * scale), 0, 0.75);
        Assert.InRange(Math.Abs(bounds.Center.Y - 15 * scale), 0, 0.75);
        if (scale == 1)
        {
            Assert.Equal(12, bounds.Width);
            Assert.Equal(12, bounds.Height);
        }
    }

    private static Rect InkBounds(SKBitmap pixels)
    {
        var minimumX = pixels.Width;
        var minimumY = pixels.Height;
        var maximumX = -1;
        var maximumY = -1;
        for (var row = 0; row < pixels.Height; row++)
        {
            for (var column = 0; column < pixels.Width; column++)
            {
                if (pixels.GetPixel(column, row).Alpha < 64) continue;
                minimumX = Math.Min(minimumX, column);
                minimumY = Math.Min(minimumY, row);
                maximumX = Math.Max(maximumX, column);
                maximumY = Math.Max(maximumY, row);
            }
        }
        Assert.True(maximumX >= minimumX && maximumY >= minimumY);
        return new Rect(minimumX, minimumY, maximumX - minimumX + 1, maximumY - minimumY + 1);
    }
}
