using Avalonia;
using Avalonia.Controls;
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
public sealed class WindowCaptionPaletteRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 1.25)]
    [InlineData(false, 1.5)]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    [InlineData(true, 1.25)]
    [InlineData(true, 1.5)]
    [InlineData(true, 2)]
    public Task CaptionInkUsesDedicatedPaletteAtEachRenderScale(bool dark, double scale) => _session.Dispatch(() =>
    {
        var window = new MainWindow
        {
            Width = 1280,
            Height = 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.MouseMove(new Point(640, 100));
            window.UpdateLayout();
            var buttons = window.GetVisualDescendants().OfType<AppIconButton>()
                .Where(button => button.Classes.Contains("window-control")).ToArray();
            Assert.Equal(3, buttons.Length);
            foreach (var button in buttons)
            {
                using var pixels = Render(button, scale);
                var ink = pixels.Pixels.Where(pixel => pixel.Alpha >= 64).ToArray();
                Assert.NotEmpty(ink);
                var expected = dark ? 255 : 31;
                Assert.All(ink, pixel =>
                {
                    AssertChannel(pixel.Red, pixel.Alpha, expected);
                    AssertChannel(pixel.Green, pixel.Alpha, expected);
                    AssertChannel(pixel.Blue, pixel.Alpha, expected);
                });
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OrdinaryMaximizeUsesSolidEdgesWithoutChangingTheReferenceInkBox(bool dark) => _session.Dispatch(() =>
    {
        var window = new MainWindow
        {
            Width = 1280,
            Height = 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.MouseMove(new Point(640, 100));
            window.UpdateLayout();
            var button = window.FindControl<AppIconButton>("MaximizeButton")!;
            using var pixels = Render(button, 1);
            var icon = Assert.IsType<AppIcon>(button.Content);
            var inkSize = InkSize(pixels);
            Assert.True(inkSize == new PixelSize(10, 10),
                $"Ink {inkSize}; icon {icon.Bounds}; origin {icon.TranslatePoint(default, button)}; center {icon.TranslatePoint(new Point(icon.Size / 2, icon.Size / 2), button)}; rounded {icon.UseLayoutRounding}.");
            Assert.Contains(pixels.Pixels, pixel => pixel.Alpha == 255);
            Assert.Equal((byte)255, pixels.GetPixel(18, 18).Alpha);
            Assert.Equal((byte)255, pixels.GetPixel(27, 18).Alpha);
            Assert.Equal((byte)255, pixels.GetPixel(23, 13).Alpha);
            Assert.Equal((byte)255, pixels.GetPixel(23, 22).Alpha);
            Assert.Equal((byte)0, pixels.GetPixel(23, 18).Alpha);
            Assert.Equal(new Size(46, 36), button.Bounds.Size);
            window.WindowState = WindowState.Maximized;
            window.UpdateLayout();
            Assert.True(button.IsAlternateIcon);
            Assert.Equal(8, Assert.IsType<AppIcon>(button.Content).Size);
            window.WindowState = WindowState.Normal;
            window.UpdateLayout();
            Assert.False(button.IsAlternateIcon);
            Assert.Equal(9, Assert.IsType<AppIcon>(button.Content).Size);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static PixelSize InkSize(SKBitmap pixels)
    {
        var columns = new List<int>();
        var rows = new List<int>();
        for (var row = 0; row < pixels.Height; row++)
            for (var column = 0; column < pixels.Width; column++)
                if (pixels.GetPixel(column, row).Alpha >= 64)
                {
                    columns.Add(column);
                    rows.Add(row);
                }
        Assert.NotEmpty(columns);
        return new PixelSize(columns.Max() - columns.Min() + 1, rows.Max() - rows.Min() + 1);
    }

    private static SKBitmap Render(AppIconButton button, double scale)
    {
        using var bitmap = new RenderTargetBitmap(
            new PixelSize((int)Math.Ceiling(46 * scale), (int)Math.Ceiling(36 * scale)), new Vector(96 * scale, 96 * scale));
        bitmap.Render(button);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return SKBitmap.Decode(stream.ToArray());
    }

    [Theory]
    [InlineData(false, 64)]
    [InlineData(false, 128)]
    [InlineData(false, 192)]
    [InlineData(false, 255)]
    [InlineData(true, 64)]
    [InlineData(true, 128)]
    [InlineData(true, 192)]
    [InlineData(true, 255)]
    public void MutedGhostPaletteCannotPassTheRenderedCaptionGuard(bool dark, byte alpha) =>
        Assert.False(MatchesChannel((byte)(dark ? 180 : 93), alpha, dark ? 255 : 31));

    private static void AssertChannel(byte actual, byte alpha, int expected) =>
        Assert.True(MatchesChannel(actual, alpha, expected), $"Expected caption channel {expected}, got {actual} at alpha {alpha}.");

    private static bool MatchesChannel(byte actual, byte alpha, int expected) =>
        Math.Abs(actual - expected) * alpha / 255d <= 2;
}
