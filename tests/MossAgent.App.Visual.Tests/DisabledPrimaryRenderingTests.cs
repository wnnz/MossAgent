using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using SkiaSharp;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class DisabledPrimaryRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, true, 1)]
    [InlineData(false, false, 1.25)]
    [InlineData(true, false, 1.25)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 2)]
    public Task DisabledPrimary_TracksThemeAndNeverExecutes(bool dark, bool composer, double scale) => _session.Dispatch(() =>
    {
        var executions = 0;
        var enabled = false;
        var command = new RelayCommand(() => executions++, () => enabled);
        var button = CreateButton(composer);
        button.Command = command;
        var outside = new AppButton { Content = "其他操作" };
        var host = new StackPanel { Margin = new Thickness(24), Spacing = 24, Children = { button, outside } };
        var window = new AppWindow
        {
            Width = 360,
            Height = 180,
            Content = host,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            window.UpdateLayout();
            var bounds = button.Bounds;
            Assert.True(outside.Focus(NavigationMethod.Pointer));
            using var disabled = Capture(window, $"primary-{composer}-{dark}-{scale}-disabled");
            AssertPalette(button, dark);
            Assert.False(button.IsEffectivelyEnabled);
            Assert.True(outside.Focus(NavigationMethod.Tab));
            Activate(window, button);
            AssertPalette(button, dark);
            Assert.Equal(0, executions);
            Assert.True(outside.IsFocused);
            window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
            window.UpdateLayout();
            using var switched = Capture(window, $"primary-{composer}-{dark}-{scale}-switched");
            AssertPalette(button, !dark);
            Assert.False(disabled.Pixels.SequenceEqual(switched.Pixels));
            Assert.Equal(bounds, button.Bounds);
            enabled = true;
            command.NotifyCanExecuteChanged();
            window.UpdateLayout();
            Assert.True(button.IsEffectivelyEnabled);
            Assert.True(button.Focus(NavigationMethod.Tab));
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            Assert.Equal(1, executions);
            enabled = false;
            command.NotifyCanExecuteChanged();
            window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            Assert.True(outside.Focus(NavigationMethod.Pointer));
            window.MouseMove(new Point(340, 160));
            window.UpdateLayout();
            using var restored = Capture(window, $"primary-{composer}-{dark}-{scale}-restored");
            AssertPalette(button, dark);
            Assert.Equal(bounds, button.Bounds);
            Assert.True(disabled.Pixels.SequenceEqual(restored.Pixels));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppButton CreateButton(bool composer)
    {
        if (!composer) return new AppButton { Classes = { "primary" }, Content = "保存配置" };
        var button = new AppIconButton { Classes = { "primary", "composer-send" }, Label = "发送消息" };
        button.IconData = Geometry.Parse("M 12 19 L 12 5 M 6 11 L 12 5 L 18 11");
        return button;
    }

    private static void AssertPalette(AppButton button, bool dark)
    {
        var background = Assert.IsAssignableFrom<ISolidColorBrush>(button.Background).Color;
        var foreground = Assert.IsAssignableFrom<ISolidColorBrush>(button.Foreground).Color;
        Assert.Equal(Color.Parse(dark ? "#3A3A3A" : "#D9D9D9"), background);
        Assert.Equal(Color.Parse(dark ? "#8B8B8B" : "#7A7A7A"), foreground);
        var presenter = Assert.Single(button.GetVisualDescendants().OfType<ContentPresenter>(), item => item.Name == "PART_ContentPresenter");
        Assert.Equal(background, Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color);
        Assert.Equal(foreground, Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Foreground).Color);
        if (button is AppIconButton)
        {
            var icon = Assert.Single(button.GetVisualDescendants().OfType<AppIcon>());
            Assert.Equal(foreground, Assert.IsAssignableFrom<ISolidColorBrush>(icon.Stroke).Color);
        }
    }

    private static void Activate(AppWindow window, AppButton button)
    {
        var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
        window.KeyRelease(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
    }

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
