using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using SkiaSharp;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class ThemeThumbnailShadowRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "light", 1)]
    [InlineData(false, "light", 1.25)]
    [InlineData(false, "light", 2)]
    [InlineData(true, "light", 1)]
    [InlineData(true, "light", 1.25)]
    [InlineData(true, "light", 2)]
    [InlineData(false, "system", 1)]
    [InlineData(false, "system", 1.25)]
    [InlineData(false, "system", 2)]
    [InlineData(true, "system", 1)]
    [InlineData(true, "system", 1.25)]
    [InlineData(true, "system", 2)]
    public Task InnerScreenShadowReachesTheBackdropWithoutPaintingOutsideTheThumbnail(
        bool dark, string mode, double scale) => _session.Dispatch(() =>
    {
        var preview = new AppThemePreview();
        preview.Classes.Add("preview-" + mode);
        var window = new AppWindow
        {
            Width = 160,
            Height = 120,
            Content = preview,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            window.UpdateLayout();
            var origin = preview.TranslatePoint(default, window)!.Value;
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var frame = window.CaptureRenderedFrame()!;
            using var stream = new MemoryStream();
            frame.Save(stream, PngBitmapEncoderOptions.Default);
            using var pixels = SKBitmap.Decode(stream.ToArray());
            var shadow = Pixel(pixels, origin, 30, 54, scale);
            Assert.InRange(shadow.Red, (byte)235, (byte)243);
            Assert.Equal(shadow.Red, shadow.Green);
            Assert.Equal(shadow.Red, shadow.Blue);
            Assert.Equal(Pixel(pixels, origin, -3, 30, scale), Pixel(pixels, origin, -9, 30, scale));
            if (mode == "system")
                Assert.Equal(SKColor.Parse("#0D0D0D"), Pixel(pixels, origin, 70, 30, scale));
            else Assert.Equal(SKColors.White, Pixel(pixels, origin, 50, 30, scale));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static SKColor Pixel(SKBitmap pixels, Point origin, int horizontal, int vertical, double scale) =>
        pixels.GetPixel((int)((origin.X + horizontal + 0.5) * scale), (int)((origin.Y + vertical + 0.5) * scale));
}
