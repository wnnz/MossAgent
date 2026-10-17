using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using SkiaSharp;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class FieldFocusRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "text", 1)]
    [InlineData(true, "text", 1)]
    [InlineData(false, "password", 1)]
    [InlineData(true, "password", 1)]
    [InlineData(false, "multiline", 1)]
    [InlineData(true, "multiline", 1)]
    [InlineData(false, "choice", 1)]
    [InlineData(true, "choice", 1)]
    [InlineData(false, "text", 1.25)]
    [InlineData(true, "text", 1.25)]
    [InlineData(false, "choice", 2)]
    [InlineData(true, "choice", 2)]
    public Task Hover_DoesNotEraseFocusedFieldBoundary(bool dark, string kind, double scale) => _session.Dispatch(() =>
    {
        var content = CreateField(kind);
        var outside = new AppButton { Content = "其他操作" };
        var host = new StackPanel { Margin = new Thickness(24), Spacing = 24, Children = { content, outside } };
        var window = new AppWindow
        {
            Width = 440,
            Height = 260,
            Content = host,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            window.UpdateLayout();
            var input = content as AppInput ?? Assert.Single(content.GetVisualDescendants().OfType<AppInput>());
            var border = Assert.Single(input.GetVisualDescendants().OfType<Border>(), item => item.Name == "PART_BorderElement");
            Assert.True(outside.Focus(NavigationMethod.Pointer));
            window.MouseMove(new Point(420, 240));
            var idle = Color(border.BorderBrush);
            var bounds = input.Bounds;
            var thickness = border.BorderThickness;
            var origin = border.TranslatePoint(default, window)!.Value;
            Assert.True(input.Focus(NavigationMethod.Tab));
            window.UpdateLayout();
            var focusedColor = Color(border.BorderBrush);
            Assert.NotEqual(idle, focusedColor);
            using var focused = Capture(window, $"field-{kind}-{dark}-{scale}-focused");
            window.MouseMove(input.TranslatePoint(new Point(input.Bounds.Width / 2, input.Bounds.Height / 2), window)!.Value);
            window.UpdateLayout();
            using var hovered = Capture(window, $"field-{kind}-{dark}-{scale}-focused-hover");
            Assert.True(input.IsPointerOver);
            Assert.True(input.IsFocused);
            Assert.Equal(focusedColor, Color(border.BorderBrush));
            Assert.Equal(Pixel(focused, origin, border.Bounds.Width, scale), Pixel(hovered, origin, border.Bounds.Width, scale));
            Assert.Equal(bounds, input.Bounds);
            Assert.Equal(thickness, border.BorderThickness);
            Assert.Equal(origin, border.TranslatePoint(default, window));
            window.KeyPress(Key.End, RawInputModifiers.Control, PhysicalKey.End, null);
            window.KeyRelease(Key.End, RawInputModifiers.Control, PhysicalKey.End, null);
            window.KeyTextInput(" custom");
            Assert.EndsWith(" custom", input.Text);
            Assert.True(outside.Focus(NavigationMethod.Pointer));
            window.UpdateLayout();
            using var blurred = Capture(window, $"field-{kind}-{dark}-{scale}-blurred-hover");
            Assert.Equal(idle, Color(border.BorderBrush));
            Assert.Equal(bounds, input.Bounds);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static Control CreateField(string kind)
    {
        if (kind == "choice") return new AppEditableChoice { Choices = ["low", "medium", "high"], SelectedChoice = "medium" };
        var input = new AppInput { Text = "model-example", PasswordChar = kind == "password" ? '●' : '\0' };
        if (kind == "multiline")
        {
            input.Classes.Add("field-multiline");
            input.AcceptsReturn = true;
        }
        return input;
    }

    private static Color Color(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static SKColor Pixel(SKBitmap bitmap, Point origin, double width, double scale) =>
        bitmap.GetPixel((int)((origin.X + width / 2) * scale), (int)((origin.Y + 0.5) * scale));

    private static SKBitmap Capture(AppWindow window, string name)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        using var stream = new MemoryStream();
        frame.Save(stream, PngBitmapEncoderOptions.Default);
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            frame.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
        }
        return SKBitmap.Decode(stream.ToArray());
    }
}
