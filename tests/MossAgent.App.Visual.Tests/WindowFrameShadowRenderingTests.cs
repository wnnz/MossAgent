using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using SkiaSharp;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class WindowFrameShadowRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 980, 640, 1)]
    [InlineData(false, 1280, 820, 1)]
    [InlineData(false, 1920, 1032, 1)]
    [InlineData(false, 980, 640, 1.25)]
    [InlineData(false, 1280, 820, 2)]
    [InlineData(true, 980, 640, 1)]
    [InlineData(true, 1280, 820, 1)]
    [InlineData(true, 1920, 1032, 1)]
    [InlineData(true, 980, 640, 1.25)]
    [InlineData(true, 1280, 820, 2)]
    public Task WorkspaceFrame_RendersShadowOutsideItsBoundsWithoutRecoloringRail(
        bool dark, double width, double height, double scale) => _session.Dispatch(() =>
    {
        var window = CreateWindow(dark, width, height);
        window.Show();
        try
        {
            window.UpdateLayout();
            var frame = window.FindControl<AppBorder>("WorkspaceFrame")!;
            var origin = frame.TranslatePoint(default, window)!.Value;
            Assert.Equal(new Point(52, 44), origin);
            using var pixels = Render(window, scale);
            var background = Sample(pixels, 4, 300, scale);
            var nearFrame = Sample(pixels, 50, 300, scale);
            Assert.True(nearFrame.Red < background.Red && nearFrame.Green < background.Green
                && nearFrame.Blue < background.Blue, $"Missing frame shadow: {nearFrame} versus {background}.");
            if (!dark && scale == 1)
            {
                Assert.InRange(background.Red - Sample(pixels, 40, 300, scale).Red, 1, 2);
                Assert.InRange(background.Red - nearFrame.Red, 5, 7);
                Assert.InRange(background.Red - Sample(pixels, 51, 300, scale).Red, 8, 11);
                int[] reference = [0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 3, 3, 4, 4, 5, 6, 10];
                for (var index = 0; index < reference.Length; index++)
                    Assert.InRange(Math.Abs(background.Red - Sample(pixels, 32 + index, 300, scale).Red - reference[index]), 0, 1);
            }
            Assert.Equal(background, Sample(pixels, 30, 300, scale));
            Assert.Equal(nearFrame, Sample(pixels, 50, 400, scale));
            var aboveFrame = Sample(pixels, 400, 43, scale);
            var titlebar = Sample(pixels, 400, 20, scale);
            Assert.True(aboveFrame.Red < titlebar.Red, $"The upper frame shadow is clipped: {aboveFrame} versus {titlebar}; "
                + string.Join("; ", frame.GetVisualAncestors().Select(visual => $"{visual.GetType().Name}: clip={visual.ClipToBounds}")));
            Assert.False(frame.ClipToBounds);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, 980, 640)]
    [InlineData(false, 1920, 1032)]
    [InlineData(true, 980, 640)]
    [InlineData(true, 1920, 1032)]
    public Task WorkspaceContent_RemainsClippedToRoundedFrame(bool dark, double width, double height) =>
        _session.Dispatch(() =>
        {
            var window = CreateWindow(dark, width, height);
            window.Show();
            try
            {
                window.UpdateLayout();
                var frame = window.FindControl<AppBorder>("WorkspaceFrame")!;
                var clip = Assert.IsType<AppBorder>(frame.Child);
                Assert.True(clip.ClipToBounds);
                Assert.Equal(frame.CornerRadius, clip.CornerRadius);
                Assert.Equal(frame.Bounds.Size, clip.Bounds.Size);
                clip.Child = new AppBorder
                {
                    Background = Brushes.Magenta,
                    RenderTransform = new ScaleTransform(1.2, 1.2)
                };
                window.UpdateLayout();
                using var pixels = Render(window, 1);
                Assert.Equal(SKColors.Magenta, Sample(pixels, 80, 80, 1));
                Assert.NotEqual(SKColors.Magenta, Sample(pixels, 50, 80, 1));
                Assert.NotEqual(SKColors.Magenta, Sample(pixels, 80, 42, 1));
                Assert.NotEqual(SKColors.Magenta, Sample(pixels, 52, 44, 1));
                Assert.NotEqual(SKColors.Magenta, Sample(pixels, width - 5, 44, 1));
                Assert.NotEqual(SKColors.Magenta, Sample(pixels, 52, height - 5, 1));
                Assert.NotEqual(SKColors.Magenta, Sample(pixels, width - 5, height - 5, 1));
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static MainWindow CreateWindow(bool dark, double width, double height) => new()
    {
        Width = width,
        Height = height,
        RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
    };

    private static SKBitmap Render(MainWindow window, double scale)
    {
        using var bitmap = new RenderTargetBitmap(
            new PixelSize((int)(window.ClientSize.Width * scale), (int)(window.ClientSize.Height * scale)),
            new Vector(96 * scale, 96 * scale));
        bitmap.Render(window);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return SKBitmap.Decode(stream.ToArray());
    }

    private static SKColor Sample(SKBitmap pixels, double column, double row, double scale) =>
        pixels.GetPixel((int)(column * scale), (int)(row * scale));
}
