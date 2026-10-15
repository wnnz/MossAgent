using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Visual.Tests;

public sealed class MarkdownSelectionRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("🙂 prefix **`first`** and _`second`_", 300, "Light")]
    [InlineData("🙂 prefix **`first`** and _`second`_", 300, "Dark")]
    [InlineData("before\n`first`\n\n`second` end", 300, "Light")]
    [InlineData("before\n`first`\n\n`second` end", 300, "Dark")]
    [InlineData("`firstsecondthirdfourthfifth`", 60, "Light")]
    [InlineData("`firstsecondthirdfourthfifth`", 60, "Dark")]
    public Task CodeBackgroundIsBelowFullAndReversePartialSelection(string markdown, double width, string theme) =>
        _session.Dispatch(() =>
        {
            var text = CreateText(markdown);
            var window = CreateWindow(text, width, theme);
            try
            {
                var content = ReadContent(text);
                var codeStart = content.IndexOf("first", StringComparison.Ordinal);
                var bounds = text.TextLayout.HitTestTextRange(codeStart, 5).Single();
                var center = bounds.Center;
                Assert.Equal(Colors.Magenta, PixelAt(text, center));
                text.SelectAll();
                Assert.Equal(content, text.SelectedText);
                Assert.Equal(0, CountColor(text, Colors.Magenta));
                Assert.Equal(Colors.Cyan, PixelAt(text, center));
                text.SelectionStart = codeStart + 4;
                text.SelectionEnd = codeStart + 1;
                Assert.Equal("irs", text.SelectedText);
                var selectedBounds = text.TextLayout.HitTestTextRange(codeStart + 1, 3).Single();
                Assert.Equal(Colors.Cyan, PixelAt(text, selectedBounds.Center));
                Assert.True(CountColor(text, Colors.Magenta) > 0);
                text.SelectionStart = text.SelectionEnd = 0;
                Assert.Equal(Colors.Magenta, PixelAt(text, center));
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CodeBackgroundUsesRoundedCornersAndMovesWithLayout() =>
        _session.Dispatch(() =>
        {
            var text = CreateText("`firstsecondthirdfourthfifth`");
            text.Padding = new Thickness(8, 6);
            var window = CreateWindow(text, 300, "Light");
            try
            {
                var bounds = text.TextLayout.HitTestTextRange(0, 25).Single();
                var top = bounds.Center.Y - 8 + text.Padding.Top;
                Assert.Equal(Colors.Magenta, PixelAt(text, bounds.Center + new Vector(8, 6)));
                var corner = new Point(bounds.X + 8, Math.Ceiling(top));
                Assert.NotEqual(Colors.Magenta, PixelAt(text, corner));
                text.CodeCornerRadius = 0;
                Assert.Equal(Colors.Magenta, PixelAt(text, corner));
                text.Width = 54;
                window.UpdateLayout();
                Assert.True(text.TextLayout.HitTestTextRange(0, 25).Count() > 1);
                text.SelectAll();
                Assert.Equal("firstsecondthirdfourthfifth", text.SelectedText);
                Assert.Equal(0, CountColor(text, Colors.Magenta));
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CodeBackgroundThemeAndContentChangesDoNotKeepOldRectangles() =>
        _session.Dispatch(() =>
        {
            var text = CreateText("plain `first` end");
            var window = CreateWindow(text, 300, "Light");
            try
            {
                Assert.True(CountColor(text, Colors.Magenta) > 0);
                text.CodeBackground = Brushes.Lime;
                Assert.Equal(0, CountColor(text, Colors.Magenta));
                Assert.True(CountColor(text, Colors.Lime) > 0);
                text.SelectAll();
                window.RequestedThemeVariant = ThemeVariant.Dark;
                window.UpdateLayout();
                Assert.Equal("plain first end", text.SelectedText);
                Assert.Equal(0, CountColor(text, Colors.Lime));
                text.SelectionStart = text.SelectionEnd = 0;
                text.Markdown = "plain replacement without code";
                window.UpdateLayout();
                Assert.Equal(0, CountColor(text, Colors.Lime));
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static MarkdownSelectableTextBlock CreateText(string markdown) => new()
    {
        Markdown = markdown,
        FontSize = 14,
        LineHeight = 23,
        Foreground = Brushes.Transparent,
        SelectionForegroundBrush = Brushes.Transparent,
        CodeBackground = Brushes.Magenta,
        SelectionBrush = Brushes.Cyan,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Top
    };

    private static Window CreateWindow(Control text, double width, string theme)
    {
        var window = new Window
        {
            Width = width,
            Height = 300,
            RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = text
        };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static string ReadContent(MarkdownSelectableTextBlock text)
    {
        text.SelectAll();
        var content = text.SelectedText;
        text.SelectionStart = text.SelectionEnd = 0;
        return content!;
    }

    private static Color PixelAt(Control text, Point point)
    {
        var pixels = Capture(text);
        var offset = ((int)point.Y * (int)Math.Ceiling(text.Bounds.Width) + (int)point.X) * 4;
        return Color.FromArgb(pixels[offset + 3], pixels[offset + 2], pixels[offset + 1], pixels[offset]);
    }

    private static int CountColor(Control text, Color color)
    {
        var pixels = Capture(text);
        var count = 0;
        for (var offset = 0; offset < pixels.Length; offset += 4)
            if (pixels[offset] == color.B && pixels[offset + 1] == color.G
                && pixels[offset + 2] == color.R && pixels[offset + 3] == color.A) count++;
        return count;
    }

    private static byte[] Capture(Control text)
    {
        var size = new PixelSize((int)Math.Ceiling(text.Bounds.Width), (int)Math.Ceiling(text.Bounds.Height));
        using var rendered = new RenderTargetBitmap(size);
        rendered.Render(text);
        using var copy = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var buffer = copy.Lock();
        rendered.CopyPixels(buffer);
        var pixels = new byte[size.Width * size.Height * 4];
        Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);
        return pixels;
    }
}
