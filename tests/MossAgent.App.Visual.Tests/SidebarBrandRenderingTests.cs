using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using SkiaSharp;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class SidebarBrandRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 1.25)]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    [InlineData(true, 1.25)]
    [InlineData(true, 2)]
    public Task BrandChevron_RendersSmallOpticallyAlignedInkAtEachScale(bool dark, double scale) => _session.Dispatch(() =>
    {
        var brand = new AppActionButton
        {
            Label = "MossAgent",
            Height = 32,
            TrailingIconData = Geometry.Parse("M 6 9 L 12 15 L 18 9"),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        brand.Classes.AddRange(["ghost", "sidebar-brand-trigger"]);
        var window = new AppWindow
        {
            Width = 264,
            Height = 64,
            Content = brand,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.MouseMove(new Point(250, 60));
            window.UpdateLayout();
            var arrow = Assert.Single(brand.GetVisualDescendants().OfType<AppIcon>(), icon => icon.IsVisible);
            Assert.Equal(6, arrow.Size);
            var origin = arrow.TranslatePoint(default, brand)!.Value;
            var arrowCenter = arrow.TranslatePoint(new Point(arrow.Bounds.Width / 2, arrow.Bounds.Height / 2), brand)!.Value;
            Assert.Equal(18.5, arrowCenter.Y);
            using var bitmap = new RenderTargetBitmap(
                new PixelSize((int)Math.Ceiling(brand.Bounds.Width * scale), (int)(32 * scale)), new Vector(96 * scale, 96 * scale));
            bitmap.Render(brand);
            using var stream = new MemoryStream();
            bitmap.Save(stream, PngBitmapEncoderOptions.Default);
            using var pixels = SKBitmap.Decode(stream.ToArray());
            var ink = InkBounds(pixels, new Rect(origin.X - 1, origin.Y - 1, 8, 8), scale);
            Assert.InRange(ink.Width, 5 * scale, 7 * scale + 1);
            Assert.InRange(ink.Height, 2 * scale, 4 * scale + 1);
            Assert.InRange(Math.Abs(ink.Center.Y / scale - 18.5), 0, 1);
            Assert.Equal(18, brand.FontSize);
            Assert.Equal(FontWeight.Bold, brand.FontWeight);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static Rect InkBounds(SKBitmap pixels, Rect region, double scale)
    {
        var minimumX = pixels.Width;
        var minimumY = pixels.Height;
        var maximumX = -1;
        var maximumY = -1;
        for (var row = Math.Max(0, (int)Math.Floor(region.Y * scale)); row < Math.Min(pixels.Height, (int)Math.Ceiling(region.Bottom * scale)); row++)
        {
            for (var column = Math.Max(0, (int)Math.Floor(region.X * scale)); column < Math.Min(pixels.Width, (int)Math.Ceiling(region.Right * scale)); column++)
            {
                if (pixels.GetPixel(column, row).Alpha < 96) continue;
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
