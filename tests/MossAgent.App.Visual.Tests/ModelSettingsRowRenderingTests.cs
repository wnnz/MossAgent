using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class ModelSettingsRowRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 320, 1)]
    [InlineData(true, 320, 1)]
    [InlineData(false, 480, 1)]
    [InlineData(true, 480, 1)]
    [InlineData(false, 640, 1)]
    [InlineData(true, 640, 1)]
    [InlineData(false, 320, 1.25)]
    [InlineData(true, 320, 1.25)]
    [InlineData(false, 640, 2)]
    [InlineData(true, 640, 2)]
    public Task NarrowRow_PrioritizesModelNameAndKeepsActionsAligned(bool dark, int width, double scale) => _session.Dispatch(() =>
    {
        var model = new ModelProfile(Guid.NewGuid(), Guid.NewGuid(), "gpt-custom-development-extended-context",
            "代码审查模型 · Extended development context", 256000, 8192,
            "extra-high-custom-reasoning", null, null, true, true, false, true, "{}");
        var row = new AppModelSettingsRow
        {
            Model = model,
            EditCommand = new RelayCommand(() => { }),
            DeleteCommand = new RelayCommand(() => { }),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        var window = new AppWindow
        {
            Width = width,
            Height = 220,
            Content = row,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            window.UpdateLayout();
            Save(window, FormattableString.Invariant($"model-row-{dark}-{width}-{scale}x"));
            var name = row.FindControl<AppText>("ModelNameText")!;
            var identity = row.FindControl<AppText>("ModelIdText")!;
            var reasoning = row.FindControl<AppText>("ReasoningText")!;
            var edit = row.FindControl<AppButton>("EditModelButton")!;
            var delete = row.FindControl<AppButton>("DeleteModelButton")!;
            Assert.True(name.Bounds.Width >= Math.Min(180, width - 130), $"Model name collapsed to {name.Bounds.Width}px");
            if (width < 480)
            {
                Assert.True(Origin(identity, row).Y >= Origin(name, row).Y + name.Bounds.Height);
                Assert.True(reasoning.Bounds.Width >= name.Bounds.Width);
            }
            Assert.Equal(model.ModelId, ToolTip.GetTip(identity));
            Assert.Equal(reasoning.Text, ToolTip.GetTip(reasoning));
            var background = Assert.IsAssignableFrom<ISolidColorBrush>(window.Background).Color;
            foreach (var text in new[] { identity, reasoning, row.FindControl<AppText>("ContextText")! })
                Assert.True(Contrast(Assert.IsAssignableFrom<ISolidColorBrush>(text.Foreground).Color, background) >= 4.5);
            Assert.InRange(Origin(name, row).X + name.Bounds.Width, 0, Origin(edit, row).X);
            Assert.InRange(Origin(delete, row).X + delete.Bounds.Width, 0, row.Bounds.Width);
            Assert.InRange(Math.Abs(Center(edit, row) - Center(delete, row)), 0, 0.5);
            Assert.Equal(24, edit.Bounds.Height);
            Assert.Equal(24, delete.Bounds.Height);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static Point Origin(Control control, Control relative) => control.TranslatePoint(default, relative)!.Value;
    private static double Center(Control control, Control relative) => Origin(control, relative).Y + control.Bounds.Height / 2;

    private static double Contrast(Color foreground, Color background)
    {
        var first = Luminance(foreground);
        var second = Luminance(background);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private static double Luminance(Color color) =>
        0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);

    private static double Linear(byte value) => value / 255d <= 0.04045
        ? value / 255d / 12.92 : Math.Pow((value / 255d + 0.055) / 1.055, 2.4);

    private static void Save(AppWindow window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        bitmap.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
