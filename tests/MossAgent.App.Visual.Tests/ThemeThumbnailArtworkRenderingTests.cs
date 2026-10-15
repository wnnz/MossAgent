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
public sealed class ThemeThumbnailArtworkRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "light", 1)]
    [InlineData(false, "light", 1.25)]
    [InlineData(false, "light", 2)]
    [InlineData(false, "dark", 1)]
    [InlineData(false, "dark", 1.25)]
    [InlineData(false, "dark", 2)]
    [InlineData(false, "system", 1)]
    [InlineData(false, "system", 1.25)]
    [InlineData(false, "system", 2)]
    [InlineData(true, "light", 1)]
    [InlineData(true, "light", 1.25)]
    [InlineData(true, "light", 2)]
    [InlineData(true, "dark", 1)]
    [InlineData(true, "dark", 1.25)]
    [InlineData(true, "dark", 2)]
    [InlineData(true, "system", 1)]
    [InlineData(true, "system", 1.25)]
    [InlineData(true, "system", 2)]
    public Task SceneInkKeepsReferenceColorsAndAnchorsIndependentOfTheApplicationTheme(
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
            Assert.Equal(new Size(80, 60), preview.Bounds.Size);
            var origin = preview.TranslatePoint(default, window)!.Value;
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var bitmap = window.CaptureRenderedFrame()!;
            using var stream = new MemoryStream();
            bitmap.Save(stream, PngBitmapEncoderOptions.Default);
            using var pixels = SKBitmap.Decode(stream.ToArray());
            using var artwork = new SKBitmap((int)(80 * scale), (int)(60 * scale));
            Assert.True(pixels.ExtractSubset(artwork, SKRectI.Create((int)(origin.X * scale),
                (int)(origin.Y * scale), artwork.Width, artwork.Height)));
            if (mode == "system")
            {
                AssertScenePixels(artwork, false, 0, scale, true);
                AssertScenePixels(artwork, true, 40, scale, true);
            }
            else AssertScenePixels(artwork, mode == "dark", 0, scale, false);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void AssertScenePixels(SKBitmap pixels, bool dark, int offset, double scale, bool clipped)
    {
        AssertPixel(pixels, 12 + offset, 30, scale, dark ? "#3D3D3D" : "#F7F7F7");
        foreach (var vertical in new[] { 18, 22, 26 })
            AssertPixel(pixels, 26 + offset, vertical, scale, dark ? "#565656" : "#E5E5E5");
        if (clipped) return;
        AssertPixel(pixels, 50, 30, scale, dark ? "#0D0D0D" : "#FFFFFF");
        foreach (var point in new[] { new Point(40, 13), new Point(35, 33), new Point(50, 37), new Point(64, 44) })
            AssertPixel(pixels, (int)point.X, (int)point.Y, scale, "#339CFF");
    }

    private static void AssertPixel(SKBitmap pixels, int horizontal, int vertical, double scale, string expected)
    {
        var actual = pixels.GetPixel((int)((horizontal + 0.5) * scale), (int)((vertical + 0.5) * scale));
        Assert.Equal(SKColor.Parse(expected), actual);
    }
}
