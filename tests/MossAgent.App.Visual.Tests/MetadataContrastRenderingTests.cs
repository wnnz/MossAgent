using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class MetadataContrastRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 1.25)]
    [InlineData(true, 1.25)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public Task SecondaryLabels_MeetTextContrastOnTheirSharedSurfaces(bool dark, double scale) => _session.Dispatch(() =>
    {
        var host = new StackPanel { Spacing = 8, Margin = new Thickness(12) };
        var window = new Window
        {
            Width = 460,
            Height = 490,
            Content = host,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            foreach (var surface in new[] { "AppBackgroundBrush", "CardBackgroundBrush", "CardSubtleBackgroundBrush", "InputBackgroundBrush", "WorkspaceSidebarBackgroundBrush", "SettingsSelectionBrush" })
                VerifyLabel(window, host, surface, "subtle", "项目与对话的辅助说明 · 今天 11:30");
            foreach (var surface in new[] { "SidebarBackgroundBrush", "WorkspaceSidebarBackgroundBrush" })
                VerifyLabel(window, host, surface, "sidebar-section-heading", "项目 · 最近");
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var bitmap = window.CaptureRenderedFrame();
            Assert.NotNull(bitmap);
            var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
            if (string.IsNullOrWhiteSpace(directory)) return;
            Directory.CreateDirectory(directory);
            bitmap.Save(Path.Combine(directory, FormattableString.Invariant($"metadata-{dark}-{scale}x.png")), PngBitmapEncoderOptions.Default);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void VerifyLabel(Window window, StackPanel host, string surface, string style, string value)
    {
        Assert.True(window.TryFindResource(surface, window.ActualThemeVariant, out var resource));
        var background = Assert.IsAssignableFrom<ISolidColorBrush>(resource);
        var label = new AppText { Text = value };
        label.Classes.Add(style);
        host.Children.Add(new AppBorder { Background = background, Padding = new Thickness(12), Child = label });
        window.UpdateLayout();
        var foreground = Assert.IsAssignableFrom<ISolidColorBrush>(label.Foreground);
        var contrast = (Math.Max(Luminance(foreground.Color), Luminance(background.Color)) + 0.05)
            / (Math.Min(Luminance(foreground.Color), Luminance(background.Color)) + 0.05);
        Assert.True(contrast >= 4.5, $"{style} on {surface} has {contrast:F2}:1 text contrast");
        Assert.InRange(label.Bounds.Height, 1, 32);
        Assert.InRange(label.Bounds.Width, 1, window.Bounds.Width);
    }

    private static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);

    private static double Linear(byte channel)
    {
        var value = channel / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
