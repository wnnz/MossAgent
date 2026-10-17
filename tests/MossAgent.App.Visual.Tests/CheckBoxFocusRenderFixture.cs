using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using MossAgent.App.Controls;
using SkiaSharp;

namespace MossAgent.App.Visual.Tests;

internal sealed class CheckBoxFocusRenderFixture : IDisposable
{
    public CheckBoxFocusRenderFixture(bool dark, bool? selected, double scale)
    {
        Scale = scale;
        CheckBox = new AppCheckBox { Content = "启用自定义代理", IsThreeState = true, IsChecked = selected };
        Outside = new AppButton { Content = "其他操作" };
        Window = new AppWindow
        {
            Width = 380,
            Height = 220,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new StackPanel { Margin = new Thickness(24), Spacing = 24, Children = { CheckBox, Outside } }
        };
        Window.Show();
        Window.SetRenderScaling(scale);
        Flush();
        Window.MouseMove(new Point(360, 200));
    }

    public double Scale { get; }
    public AppCheckBox CheckBox { get; }
    public AppButton Outside { get; }
    public AppWindow Window { get; }

    public void Flush()
    {
        Window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    public SKBitmap Capture(string state)
    {
        Flush();
        using var frame = Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rendered frame.");
        using var stream = new MemoryStream();
        frame.Save(stream, PngBitmapEncoderOptions.Default);
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            frame.Save(Path.Combine(directory, $"checkbox-focus-{CheckBox.IsChecked}-{Window.ActualThemeVariant}-{Scale}-{state}.png"), PngBitmapEncoderOptions.Default);
        }
        return SKBitmap.Decode(stream.ToArray());
    }

    public SKColor Pixel(SKBitmap bitmap, Point offset)
    {
        var point = CheckBox.TranslatePoint(offset, Window)!.Value;
        return bitmap.GetPixel((int)(point.X * Scale), (int)(point.Y * Scale));
    }

    public void Space()
    {
        Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        Window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        Flush();
    }

    public void Dispose() => Window.Close();
}
