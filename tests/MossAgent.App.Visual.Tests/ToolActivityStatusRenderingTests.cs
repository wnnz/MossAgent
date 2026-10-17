using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class ToolActivityStatusRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    public static IEnumerable<object[]> Cases()
    {
        foreach (var dark in new[] { false, true })
            foreach (var expandable in new[] { false, true })
                foreach (var scale in new[] { 1d, 1.25, 2 })
                    yield return [dark, expandable, scale];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public Task Status_HasReadableContrastAndSpaceInsideKeyboardFocus(bool dark, bool expandable, double scale) => _session.Dispatch(() =>
    {
        var row = new AppToolActivityRow
        {
            Summary = "读取项目中的很长的文件名称以检查边界和工具输出布局.txt",
            Status = "等待用户批准",
            CanExpand = expandable,
            Command = new RelayCommand(() => { }),
            Margin = new Thickness(12),
            VerticalAlignment = VerticalAlignment.Top
        };
        var window = new Window
        {
            Content = row,
            Width = 280,
            Height = 80,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            window.UpdateLayout();
            if (expandable) Assert.True(row.FindControl<AppButton>("ToolButton")!.Focus(NavigationMethod.Tab));
            var status = Assert.Single(row.GetVisualDescendants().OfType<AppText>(), text => text.IsEffectivelyVisible && text.Text == row.Status);
            var foreground = Assert.IsAssignableFrom<ISolidColorBrush>(status.Foreground).Color;
            Assert.True(window.TryFindResource("AppBackgroundBrush", window.ActualThemeVariant, out var resource));
            var background = Assert.IsAssignableFrom<ISolidColorBrush>(resource).Color;
            var contrast = (Math.Max(Luminance(foreground), Luminance(background)) + 0.05)
                / (Math.Min(Luminance(foreground), Luminance(background)) + 0.05);
            Assert.True(contrast >= 4.5, $"Status contrast is {contrast:F2}:1");
            var origin = status.TranslatePoint(default, row)!.Value;
            var inset = row.Bounds.Width - origin.X - status.Bounds.Width;
            Assert.InRange(Math.Abs(inset - 6) * scale, 0, 0.500001);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var bitmap = window.CaptureRenderedFrame();
            Assert.NotNull(bitmap);
            var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
            if (string.IsNullOrWhiteSpace(directory)) return;
            Directory.CreateDirectory(directory);
            bitmap.Save(Path.Combine(directory, FormattableString.Invariant($"tool-status-{dark}-{expandable}-{scale}x.png")), PngBitmapEncoderOptions.Default);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);

    private static double Linear(byte channel)
    {
        var value = channel / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
