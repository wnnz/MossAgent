using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using MossAgent.App.Controls;
using SkiaSharp;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class ProjectTaskPreviewRenderingTests : IAsyncLifetime
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
    public Task ExpansionLabelUsesSharedSidebarTypographyAndReadableInk(bool dark, double scale) => _session.Dispatch(() =>
    {
        var button = new AppButton
        {
            Content = "展开显示",
            Width = 238
        };
        button.Classes.AddRange(["ghost", "sidebar-project-more", "align-leading"]);
        var window = new AppWindow
        {
            Width = 264,
            Height = 64,
            Content = button,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.MouseMove(new Point(250, 60));
            window.UpdateLayout();
            Assert.Equal(14, button.FontSize);
            Assert.Equal(30, button.Bounds.Height);
            Assert.Equal(new Thickness(32, 0, 8, 0), button.Padding);
            Assert.Equal(default, button.Margin);
            var expected = dark ? Color.Parse("#8E8E8E") : Color.Parse("#8B8D8F");
            Assert.Equal(expected, Assert.IsAssignableFrom<ISolidColorBrush>(button.Foreground).Color);
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(238 * scale), (int)Math.Ceiling(30 * scale)),
                new Vector(96 * scale, 96 * scale));
            bitmap.Render(button);
            using var stream = new MemoryStream();
            bitmap.Save(stream, PngBitmapEncoderOptions.Default);
            using var pixels = SKBitmap.Decode(stream.ToArray());
            AssertLabelInk(pixels, expected, scale);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void AssertLabelInk(SKBitmap pixels, Color expected, double scale)
    {
        var left = pixels.Width;
        var right = -1;
        var matching = 0;
        for (var vertical = 0; vertical < pixels.Height; vertical++)
            for (var horizontal = 0; horizontal < pixels.Width; horizontal++)
            {
                var color = pixels.GetPixel(horizontal, vertical);
                if (color.Alpha < 240 || color.Red > 205 || color.Green > 205 || color.Blue > 205) continue;
                left = Math.Min(left, horizontal);
                right = Math.Max(right, horizontal);
                if (Math.Abs(color.Red - expected.R) <= 1 && Math.Abs(color.Green - expected.G) <= 1
                    && Math.Abs(color.Blue - expected.B) <= 1) matching++;
            }
        Assert.InRange(left / scale, 31, 34);
        Assert.InRange((right - left + 1) / scale, 54, 58);
        Assert.True(matching > 10);
    }
}
