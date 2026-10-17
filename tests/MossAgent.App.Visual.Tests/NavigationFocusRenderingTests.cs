using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using SkiaSharp;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class NavigationFocusRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "settings", false)]
    [InlineData(true, "settings", false)]
    [InlineData(false, "settings", true)]
    [InlineData(true, "settings", true)]
    [InlineData(false, "history", false)]
    [InlineData(true, "history", false)]
    [InlineData(false, "disclosure", false)]
    [InlineData(true, "disclosure", false)]
    public Task KeyboardFocus_IsVisibleWithoutChangingLayout(bool dark, string kind, bool selected) => _session.Dispatch(() =>
    {
        var button = CreateButton(kind, selected);
        button.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        var outside = new AppButton { Content = "其他操作" };
        var host = new StackPanel { Margin = new Thickness(20), Spacing = 20, Children = { button, outside } };
        var window = new AppWindow
        {
            Width = 320,
            Height = 180,
            Content = host,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            Assert.True(button.Focus(NavigationMethod.Pointer));
            window.UpdateLayout();
            var bounds = button.Bounds;
            var label = button.GetVisualDescendants().OfType<TextBlock>().First();
            var textOrigin = label.TranslatePoint(default, window);
            using var pointer = Capture(window, $"navigation-{kind}-{selected}-{dark}-pointer");
            Assert.True(outside.Focus(NavigationMethod.Pointer));
            Assert.True(button.Focus(NavigationMethod.Tab));
            window.UpdateLayout();
            using var focused = Capture(window, $"navigation-{kind}-{selected}-{dark}-keyboard");
            Assert.True(button.IsKeyboardFocusWithin);
            Assert.False(pointer.Pixels.SequenceEqual(focused.Pixels), $"{kind} has no visible keyboard focus");
            Assert.Equal(bounds, button.Bounds);
            Assert.Equal(textOrigin, label.TranslatePoint(default, window));
            Assert.True(outside.Focus(NavigationMethod.Pointer));
            using var blurred = Capture(window, $"navigation-{kind}-{selected}-{dark}-blur");
            Assert.True(pointer.Pixels.SequenceEqual(blurred.Pixels), "Focus feedback remains after blur");
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static Button CreateButton(string kind, bool selected) => kind switch
    {
        "settings" => new AppToggleButton { Classes = { "settings-nav" }, Content = "AI 提供商", IsChecked = selected },
        "history" => new AppButton { Classes = { "ghost", "navigation-history" }, Content = "返回" },
        _ => new AppToggleButton { Classes = { "disclosure-trigger" }, Content = "高级设置" }
    };

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
