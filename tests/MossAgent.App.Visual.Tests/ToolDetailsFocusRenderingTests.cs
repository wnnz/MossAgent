using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using MossAgent.App.Controls;
using SkiaSharp;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class ToolDetailsFocusRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 280, 1)]
    [InlineData(true, 280, 1)]
    [InlineData(false, 740, 1)]
    [InlineData(true, 740, 1)]
    [InlineData(false, 280, 1.25)]
    [InlineData(true, 280, 1.25)]
    [InlineData(false, 740, 2)]
    [InlineData(true, 740, 2)]
    public Task KeyboardFocus_UsesTheRoundedCardWithoutLayoutOrScopeChanges(bool dark, int width, double scale) => _session.Dispatch(() =>
    {
        var viewer = new AppScrollViewer
        {
            Classes = { "tool-details-scroll" },
            Content = new AppText { Text = string.Join('\n', Enumerable.Repeat("工具输出", 40)) }
        };
        var card = new AppBorder { Classes = { "tool-detail-surface" }, Child = viewer };
        var outside = new AppButton { Content = "其他操作" };
        var unrelated = new AppScrollViewer { Content = new AppText { Text = "普通内容区" } };
        var host = new StackPanel { Margin = new Thickness(16), Spacing = 12, Children = { card, outside, unrelated } };
        var window = new AppWindow
        {
            Width = width,
            Height = 400,
            Content = host,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        window.SetRenderScaling(scale);
        try
        {
            Assert.True(outside.Focus(NavigationMethod.Pointer));
            window.UpdateLayout();
            var bounds = card.Bounds;
            var origin = card.TranslatePoint(default, window)!.Value;
            using var before = Capture(window, $"tool-focus-{dark}-{width}-{scale}-before");
            Assert.True(viewer.Focus(NavigationMethod.Tab));
            window.UpdateLayout();
            using var focused = Capture(window, $"tool-focus-{dark}-{width}-{scale}-focused");
            Assert.Equal(new CornerRadius(12), card.CornerRadius);
            Assert.Equal(new Thickness(1), card.BorderThickness);
            Assert.Equal(bounds, card.Bounds);
            Assert.Null(viewer.FocusAdorner);
            Assert.False(unrelated.Focusable);
            var edge = new Point(origin.X + card.Bounds.Width / 2, origin.Y + 0.5);
            Assert.NotEqual(Pixel(before, edge, scale), Pixel(focused, edge, scale));
            var corner = new Point(origin.X + 0.5, origin.Y + 0.5);
            Assert.Equal(Pixel(before, corner, scale), Pixel(focused, corner, scale));
            Assert.True(outside.Focus(NavigationMethod.Pointer));
            using var blurred = Capture(window, $"tool-focus-{dark}-{width}-{scale}-blurred");
            Assert.True(before.Pixels.SequenceEqual(blurred.Pixels));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static SKColor Pixel(SKBitmap bitmap, Point point, double scale) =>
        bitmap.GetPixel((int)(point.X * scale), (int)(point.Y * scale));

    private static SKBitmap Capture(AppWindow window, string name)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            bitmap.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
        }
        return SKBitmap.Decode(stream.ToArray());
    }
}
