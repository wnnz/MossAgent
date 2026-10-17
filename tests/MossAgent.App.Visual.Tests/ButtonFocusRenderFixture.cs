using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using MossAgent.App.Controls;
using SkiaSharp;

namespace MossAgent.App.Visual.Tests;

internal sealed class ButtonFocusRenderFixture : IDisposable
{
    public ButtonFocusRenderFixture(bool dark, string kind, double scale)
    {
        Kind = kind;
        Scale = scale;
        Button = CreateButton(kind);
        Button.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        Outside = new AppButton { Content = "其他操作" };
        Window = new AppWindow
        {
            Width = 360,
            Height = 220,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new StackPanel { Margin = new Thickness(24), Spacing = 24, Children = { Button, Outside } }
        };
        Window.Show();
        Window.SetRenderScaling(scale);
        Flush();
        Window.MouseMove(new Point(340, 200));
    }

    public string Kind { get; }
    public double Scale { get; }
    public Button Button { get; }
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
            frame.Save(Path.Combine(directory, $"button-focus-{Kind}-{Window.ActualThemeVariant}-{Scale}-{state}.png"), PngBitmapEncoderOptions.Default);
        }
        return SKBitmap.Decode(stream.ToArray());
    }

    public SKColor Pixel(SKBitmap bitmap, Point offset)
    {
        var point = Button.TranslatePoint(offset, Window)!.Value;
        return bitmap.GetPixel((int)(point.X * Scale), (int)(point.Y * Scale));
    }

    public void Key(Key key, PhysicalKey physical)
    {
        Window.KeyPress(key, RawInputModifiers.None, physical, null);
        Window.KeyRelease(key, RawInputModifiers.None, physical, null);
        Flush();
    }

    public void Dispose() => Window.Close();

    private static Button CreateButton(string kind) => kind switch
    {
        "primary" => new AppButton { Content = "保存", Classes = { "primary" } },
        "ghost" => new AppButton { Content = "取消", Classes = { "ghost" } },
        "danger" => new AppButton { Content = "删除", Classes = { "danger" } },
        "icon" => new AppIconButton { Label = "刷新", IconData = Geometry.Parse("M 5 12 L 19 12 M 12 5 L 12 19") },
        "history" => new AppButton { Content = "返回", Classes = { "ghost", "navigation-history" } },
        "settings" => new AppToggleButton { Content = "AI 提供商", Classes = { "settings-nav" }, IsChecked = true },
        "send" => new AppIconButton { Label = "发送", Classes = { "primary", "composer-send" }, IconData = Geometry.Parse("M 12 19 L 12 5 M 5 12 L 12 5 L 19 12") },
        "rail" => new AppIconButton { Label = "定时任务", Classes = { "navigation-rail-button", "selected" }, IconData = Geometry.Parse("M 12 3 A 9 9 0 1 1 12 21 A 9 9 0 1 1 12 3 Z") },
        "conversation" => new AppButton { Content = "当前对话", Classes = { "conversation-tab", "selected" } },
        _ => new AppButton { Content = "运行" }
    };
}
