using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class ToolActivityGlyphRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task Terminal_HasCompactSquareInkAndRoundedCorners(bool dark, bool expandable) => _session.Dispatch(() =>
    {
        var row = new AppToolActivityRow
        {
            Summary = "运行工具",
            Status = "完成",
            CanExpand = expandable,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(12)
        };
        var surface = new Panel();
        surface.Children.Add(row);
        var window = new Window
        {
            Content = surface,
            Width = 280,
            Height = 80,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var glyph = Assert.Single(row.GetVisualDescendants().OfType<AppIcon>(),
                icon => icon.IsEffectivelyVisible && !icon.Classes.Contains("tool-expander"));
            var origin = glyph.TranslatePoint(default, surface)!.Value;
            var size = new PixelSize((int)surface.Bounds.Width, (int)surface.Bounds.Height);
            using var rendered = new RenderTargetBitmap(size);
            rendered.Render(surface);
            using var copy = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using var buffer = copy.Lock();
            rendered.CopyPixels(buffer);
            var pixels = new byte[buffer.RowBytes * size.Height];
            Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);
            VerifyInk(pixels, buffer.RowBytes, origin);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void VerifyInk(byte[] pixels, int stride, Point origin)
    {
        var startX = (int)Math.Floor(origin.X) - 1;
        var startY = (int)Math.Floor(origin.Y) - 1;
        var points = new List<PixelPoint>();
        for (var vertical = startY; vertical < startY + 14; vertical++)
            for (var horizontal = startX; horizontal < startX + 14; horizontal++)
                if (pixels[vertical * stride + horizontal * 4 + 3] > 0) points.Add(new(horizontal, vertical));
        Assert.NotEmpty(points);
        Assert.Equal(14, points.Max(point => point.X) - points.Min(point => point.X) + 1);
        Assert.Equal(14, points.Max(point => point.Y) - points.Min(point => point.Y) + 1);
        Assert.DoesNotContain(new PixelPoint(startX, startY), points);
        Assert.DoesNotContain(new PixelPoint(startX + 13, startY), points);
    }
}
